using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface ISensorService
{
    Task<IReadOnlyList<SensorResponse>> ListarAsync(Guid? bairroId, TipoSensor? tipo, CancellationToken ct);
    Task<SensorResponse> ObterAsync(Guid id, CancellationToken ct);
    Task<SensorResponse> CriarAsync(CriarSensorRequest request, CancellationToken ct);
    Task<SensorResponse> AlterarAtivoAsync(Guid id, bool ativo, CancellationToken ct);
    Task<LeituraProcessadaResponse> RegistrarLeituraAsync(Guid sensorId, decimal valor, CancellationToken ct);
    Task<PaginaResponse<LeituraResponse>> ListarLeiturasAsync(Guid sensorId, int pagina, int tamanhoPagina, CancellationToken ct);
}

public class SensorService(IAppDbContext db, IDespachoMissoes despacho, TimeProvider relogio) : ISensorService
{
    public async Task<IReadOnlyList<SensorResponse>> ListarAsync(Guid? bairroId, TipoSensor? tipo, CancellationToken ct)
    {
        var query = db.Sensores.AsNoTracking();
        if (bairroId is not null) query = query.Where(s => s.BairroId == bairroId);
        if (tipo is not null) query = query.Where(s => s.Tipo == tipo);
        return (await query.OrderBy(s => s.Codigo).ToListAsync(ct)).Select(s => s.ToResponse()).ToList();
    }

    public async Task<SensorResponse> ObterAsync(Guid id, CancellationToken ct) =>
        (await BuscarAsync(id, ct)).ToResponse();

    public async Task<SensorResponse> CriarAsync(CriarSensorRequest r, CancellationToken ct)
    {
        var bairroId = r.BairroId!.Value;
        if (!await db.Bairros.AnyAsync(b => b.Id == bairroId, ct))
            throw new RecursoNaoEncontradoException("Bairro", bairroId);

        if (r.QuadraId is not null)
        {
            var quadra = await db.Quadras.FirstOrDefaultAsync(q => q.Id == r.QuadraId, ct)
                         ?? throw new RecursoNaoEncontradoException("Quadra", r.QuadraId);
            if (quadra.BairroId != bairroId)
                throw new RegraNegocioException("A quadra informada não pertence ao bairro do sensor.");
        }

        var codigo = r.Codigo.Trim().ToUpperInvariant();
        if (await db.Sensores.AnyAsync(s => s.Codigo == codigo, ct))
            throw new RegraNegocioException($"Já existe um sensor com o código '{codigo}'.");

        var sensor = new Sensor(codigo, r.Tipo!.Value, bairroId, r.QuadraId, r.Limiar);
        db.Sensores.Add(sensor);
        await db.SaveChangesAsync(ct);
        return sensor.ToResponse();
    }

    public async Task<SensorResponse> AlterarAtivoAsync(Guid id, bool ativo, CancellationToken ct)
    {
        var sensor = await BuscarAsync(id, ct);
        if (ativo) sensor.Ativar(); else sensor.Desativar();
        await db.SaveChangesAsync(ct);
        return sensor.ToResponse();
    }

    /// <summary>
    /// Registra a leitura e reage a ela:
    /// boca de lobo acima do limiar → missão de desobstrução (com robô, se houver);
    /// nível da quadra → atualiza a quadra e alerta quando ela chega a 90%.
    /// </summary>
    public async Task<LeituraProcessadaResponse> RegistrarLeituraAsync(Guid sensorId, decimal valor, CancellationToken ct)
    {
        var sensor = await BuscarAsync(sensorId, ct);
        var leitura = sensor.RegistrarLeitura(valor, relogio.GetUtcNow());
        db.Leituras.Add(leitura);

        var acoes = sensor.Tipo switch
        {
            TipoSensor.BocaDeLobo => await ReagirBocaDeLoboAsync(sensor, valor, ct),
            TipoSensor.NivelQuadra => await ReagirNivelQuadraAsync(sensor, valor, ct),
            _ => []
        };

        await db.SaveChangesAsync(ct);
        return new LeituraProcessadaResponse(leitura.ToResponse(sensor.Tipo), acoes);
    }

    public async Task<PaginaResponse<LeituraResponse>> ListarLeiturasAsync(Guid sensorId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var sensor = await BuscarAsync(sensorId, ct);
        pagina = Math.Max(1, pagina);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, 100);

        var query = db.Leituras.AsNoTracking().Where(l => l.SensorId == sensorId);
        var total = await query.CountAsync(ct);
        var itens = await query.OrderByDescending(l => l.RegistradaEm)
            .Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToListAsync(ct);

        return new PaginaResponse<LeituraResponse>(itens.Select(l => l.ToResponse(sensor.Tipo)).ToList(), pagina, tamanhoPagina, total);
    }

    private async Task<List<string>> ReagirBocaDeLoboAsync(Sensor sensor, decimal valor, CancellationToken ct)
    {
        if (!sensor.UltrapassaLimiar(valor))
            return [];

        var jaAberta = await db.Missoes.AnyAsync(m => m.SensorId == sensor.Id && m.Status != StatusMissao.Concluida, ct);
        if (jaAberta)
            return [$"Entupimento em {sensor.Codigo} ({valor}%) já possui missão de desobstrução aberta."];

        var riscoAlto = await db.AvaliacoesRisco
            .Where(a => a.BairroId == sensor.BairroId)
            .OrderByDescending(a => a.CriadoEm)
            .Select(a => (NivelRisco?)a.Nivel)
            .FirstOrDefaultAsync(ct) == NivelRisco.Alto;

        var missao = await despacho.CriarEDespacharAsync(
            TipoMissao.Desobstrucao, sensor.BairroId, sensor.Id,
            riscoAlto ? PrioridadeMissao.Alta : PrioridadeMissao.Normal,
            $"Desobstruir boca de lobo do sensor {sensor.Codigo} ({valor}% de obstrução).", ct);

        return missao.Robo is not null
            ? [$"Entupimento detectado em {sensor.Codigo} ({valor}%): missão criada e atribuída ao robô {missao.Robo.Codigo}."]
            : [$"Entupimento detectado em {sensor.Codigo} ({valor}%): missão criada e aguardando robô disponível."];
    }

    private async Task<List<string>> ReagirNivelQuadraAsync(Sensor sensor, decimal valor, CancellationToken ct)
    {
        var quadra = await db.Quadras.FirstAsync(q => q.Id == sensor.QuadraId, ct);
        var statusAnterior = quadra.Status;
        quadra.AtualizarNivel(valor);

        var acoes = new List<string> { $"Nível da quadra {quadra.Nome} atualizado para {valor}% ({quadra.VolumeRetidoM3} m³)." };
        if (statusAnterior != quadra.Status)
            acoes.Add($"{quadra.Nome} passou de {statusAnterior} para {quadra.Status}.");

        if (quadra.ProximaDaCapacidade)
        {
            var alertaExistente = await db.Alertas.AnyAsync(a => a.QuadraId == quadra.Id && a.Ativo, ct);
            if (!alertaExistente)
            {
                db.Alertas.Add(new Alerta(quadra.BairroId, quadra.Id, NivelRisco.Alto,
                    $"{quadra.Nome} atingiu {valor}% da capacidade. Prepare a liberação controlada da água."));
                acoes.Add($"Alerta criado: {quadra.Nome} perto da capacidade.");
            }
        }

        return acoes;
    }

    private async Task<Sensor> BuscarAsync(Guid id, CancellationToken ct) =>
        await db.Sensores.FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new RecursoNaoEncontradoException("Sensor", id);
}

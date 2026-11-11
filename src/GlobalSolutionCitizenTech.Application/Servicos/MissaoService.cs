using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface IMissaoService
{
    Task<IReadOnlyList<MissaoResponse>> ListarAsync(StatusMissao? status, Guid? bairroId, CancellationToken ct);
    Task<MissaoResponse> ObterAsync(Guid id, CancellationToken ct);
    Task<MissaoResponse> CriarAsync(CriarMissaoRequest request, CancellationToken ct);
    Task<MissaoConcluidaResponse> ConcluirAsync(Guid id, CancellationToken ct);
}

public class MissaoService(IAppDbContext db, IDespachoMissoes despacho, TimeProvider relogio) : IMissaoService
{
    public async Task<IReadOnlyList<MissaoResponse>> ListarAsync(StatusMissao? status, Guid? bairroId, CancellationToken ct)
    {
        var query = db.Missoes.AsNoTracking().Include(m => m.Robo).AsQueryable();
        if (status is not null) query = query.Where(m => m.Status == status);
        if (bairroId is not null) query = query.Where(m => m.BairroId == bairroId);

        var missoes = await query.ToListAsync(ct);
        return missoes
            .OrderByDescending(m => m.Prioridade)
            .ThenBy(m => m.CriadoEm)
            .Select(m => m.ToResponse())
            .ToList();
    }

    public async Task<MissaoResponse> ObterAsync(Guid id, CancellationToken ct) =>
        (await BuscarAsync(id, ct)).ToResponse();

    /// <summary>Abre uma missão manual (ex.: inspeção preventiva) e já tenta despachar um robô.</summary>
    public async Task<MissaoResponse> CriarAsync(CriarMissaoRequest r, CancellationToken ct)
    {
        var bairroId = r.BairroId!.Value;
        if (!await db.Bairros.AnyAsync(b => b.Id == bairroId, ct))
            throw new RecursoNaoEncontradoException("Bairro", bairroId);

        if (r.SensorId is not null)
        {
            var sensor = await db.Sensores.FirstOrDefaultAsync(s => s.Id == r.SensorId, ct)
                         ?? throw new RecursoNaoEncontradoException("Sensor", r.SensorId);
            if (sensor.BairroId != bairroId)
                throw new RegraNegocioException("O sensor informado não pertence ao bairro da missão.");
        }

        var tipo = r.Tipo!.Value;
        var descricao = string.IsNullOrWhiteSpace(r.Descricao)
            ? (tipo == TipoMissao.Inspecao ? "Inspeção preventiva das galerias." : "Desobstrução de drenagem.")
            : r.Descricao.Trim();

        var missao = await despacho.CriarEDespacharAsync(tipo, bairroId, r.SensorId, r.Prioridade, descricao, ct);
        await db.SaveChangesAsync(ct);
        return missao.ToResponse();
    }

    /// <summary>Conclui a missão; o robô volta a ficar disponível e já assume a próxima pendente do bairro.</summary>
    public async Task<MissaoConcluidaResponse> ConcluirAsync(Guid id, CancellationToken ct)
    {
        var missao = await BuscarAsync(id, ct);
        missao.Concluir(relogio.GetUtcNow());

        var proxima = await despacho.AtribuirProximaPendenteAsync(missao.Robo!, ct);
        await db.SaveChangesAsync(ct);
        return new MissaoConcluidaResponse(missao.ToResponse(), proxima?.ToResponse());
    }

    private async Task<Missao> BuscarAsync(Guid id, CancellationToken ct) =>
        await db.Missoes.Include(m => m.Robo).FirstOrDefaultAsync(m => m.Id == id, ct)
        ?? throw new RecursoNaoEncontradoException("Missão", id);
}

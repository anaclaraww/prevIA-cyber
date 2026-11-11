using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Application.Simulacao;

public enum CenarioSimulacao
{
    /// <summary>Tempo seco: pouca chuva, lixo acumulando devagar nas bocas de lobo.</summary>
    Normal = 1,
    /// <summary>Temporal: chuva intensa, bocas de lobo entupindo rápido, quadras enchendo.</summary>
    ChuvaForte = 2
}

/// <summary>Seção <c>Simulador</c> do appsettings.</summary>
public class ConfiguracaoSimulador
{
    public const string Secao = "Simulador";

    /// <summary>Liga o ciclo automático em background. O endpoint de ciclo manual funciona sempre.</summary>
    public bool Habilitado { get; set; }
    public int IntervaloSegundos { get; set; } = 20;
    public CenarioSimulacao Cenario { get; set; } = CenarioSimulacao.Normal;

    /// <summary>A cada quantos ciclos reavaliar o risco dos bairros (0 = nunca).</summary>
    public int AvaliarRiscoACadaCiclos { get; set; } = 3;

    /// <summary>Tempo que um robô leva para concluir uma missão.</summary>
    public int DuracaoMissaoSegundos { get; set; } = 60;

    /// <summary>Semente do gerador aleatório, para uma simulação reproduzível.</summary>
    public int? Semente { get; set; }

    /// <summary>Se definido, substitui a previsão do Open-Meteo por este valor (mm). Útil para demonstrar risco alto.</summary>
    public decimal? ChuvaPrevistaMm { get; set; }
}

/// <summary>Estado que sobrevive entre ciclos (singleton): valores atuais de cada sensor e o gerador aleatório.</summary>
public sealed class EstadoSimulacao(IOptions<ConfiguracaoSimulador> config)
{
    private readonly Dictionary<Guid, decimal> _valores = [];
    private readonly Random _aleatorio = config.Value.Semente is { } semente ? new Random(semente) : new Random();

    /// <summary>Um ciclo por vez, seja do background ou do endpoint.</summary>
    public SemaphoreSlim Trava { get; } = new(1, 1);
    public int Ciclos { get; private set; }

    public int NovoCiclo() => ++Ciclos;

    public decimal Valor(Guid sensorId, Func<decimal> inicial) =>
        _valores.TryGetValue(sensorId, out var v) ? v : _valores[sensorId] = inicial();

    public void Definir(Guid sensorId, decimal valor) => _valores[sensorId] = valor;

    public decimal Entre(double min, double max) => Math.Round((decimal)(min + _aleatorio.NextDouble() * (max - min)), 1);
}

public interface ISimuladorSensores
{
    Task<ResultadoCicloSimulacaoResponse> ExecutarCicloAsync(CancellationToken ct);
    StatusSimuladorResponse Status();
}

/// <summary>
/// Simula a cidade em funcionamento. A cada ciclo:
/// <list type="number">
/// <item>robôs concluem as missões que já duraram o suficiente e os sem bateria são recarregados na base;</item>
/// <item>cada sensor ativo gera uma leitura plausível para o cenário, enviada pelo mesmo fluxo da API
/// (e, por isso, dispara as mesmas reações: missões, mudança de status da quadra, alertas);</item>
/// <item>quadras esvaziadas voltam ao uso;</item>
/// <item>de tempos em tempos, o risco de cada bairro é reavaliado.</item>
/// </list>
/// </summary>
public class SimuladorSensores(
    IAppDbContext db,
    ISensorService sensores,
    IMissaoService missoes,
    IRoboService robos,
    IQuadraService quadras,
    IRiscoService risco,
    EstadoSimulacao estado,
    IOptions<ConfiguracaoSimulador> opcoes,
    TimeProvider relogio) : ISimuladorSensores
{
    private ConfiguracaoSimulador Config => opcoes.Value;
    private bool ChuvaForte => Config.Cenario == CenarioSimulacao.ChuvaForte;

    public StatusSimuladorResponse Status() =>
        new(Config.Habilitado, Config.IntervaloSegundos, Config.Cenario, estado.Ciclos, Config.ChuvaPrevistaMm);

    public async Task<ResultadoCicloSimulacaoResponse> ExecutarCicloAsync(CancellationToken ct)
    {
        await estado.Trava.WaitAsync(ct);
        try
        {
            var ciclo = estado.NovoCiclo();
            var acoes = new List<string>();
            var leituras = new List<LeituraSimuladaResponse>();

            await AvancarRobosAsync(acoes, ct);
            await GerarLeiturasAsync(leituras, acoes, ct);
            await LiberarQuadrasVaziasAsync(acoes, ct);
            if (Config.AvaliarRiscoACadaCiclos > 0 && ciclo % Config.AvaliarRiscoACadaCiclos == 0)
                await AvaliarRiscoAsync(acoes, ct);

            return new ResultadoCicloSimulacaoResponse(ciclo, relogio.GetUtcNow(), Config.Cenario, leituras, acoes);
        }
        finally
        {
            estado.Trava.Release();
        }
    }

    private async Task AvancarRobosAsync(List<string> acoes, CancellationToken ct)
    {
        var limite = relogio.GetUtcNow().AddSeconds(-Config.DuracaoMissaoSegundos);
        var prontas = (await db.Missoes.AsNoTracking()
                .Where(m => m.Status == StatusMissao.EmAndamento)
                .Select(m => new { m.Id, m.IniciadaEm })
                .ToListAsync(ct))
            .Where(m => m.IniciadaEm <= limite)
            .Select(m => m.Id)
            .ToList();

        foreach (var id in prontas)
        {
            var resultado = await missoes.ConcluirAsync(id, ct);
            acoes.Add($"Robô {resultado.Missao.RoboCodigo} concluiu: {resultado.Missao.Descricao}");
            if (resultado.ProximaMissaoDoRobo is { } proxima)
                acoes.Add($"Robô {proxima.RoboCodigo} assumiu a próxima missão: {proxima.Descricao}");
        }

        var semBateria = await db.Robos.AsNoTracking()
            .Where(r => r.Status == StatusRobo.Disponivel && r.Bateria < Robo.BateriaMinimaParaMissao)
            .Select(r => new { r.Id, r.Codigo })
            .ToListAsync(ct);
        foreach (var robo in semBateria)
        {
            var recarregado = await robos.RecarregarAsync(robo.Id, ct);
            acoes.Add(recarregado.Status == StatusRobo.EmMissao
                ? $"Robô {robo.Codigo} recarregado na base e já saiu para uma missão pendente."
                : $"Robô {robo.Codigo} recarregado na base.");
        }
    }

    private async Task GerarLeiturasAsync(List<LeituraSimuladaResponse> leituras, List<string> acoes, CancellationToken ct)
    {
        var ativos = await db.Sensores.AsNoTracking().Where(s => s.Ativo).OrderBy(s => s.Codigo).ToListAsync(ct);
        foreach (var sensor in ativos)
        {
            var valor = sensor.Tipo switch
            {
                TipoSensor.Pluviometro => ProximaChuva(sensor),
                TipoSensor.BocaDeLobo => await ProximaObstrucaoAsync(sensor, ct),
                TipoSensor.NivelQuadra => await ProximoNivelAsync(sensor, ct),
                _ => null
            };
            if (valor is null)
                continue;

            try
            {
                var resultado = await sensores.RegistrarLeituraAsync(sensor.Id, valor.Value, ct);
                leituras.Add(new LeituraSimuladaResponse(sensor.Codigo, sensor.Tipo, valor.Value, sensor.Unidade));
                acoes.AddRange(resultado.AcoesExecutadas.Where(a => !a.Contains("já possui missão")));
            }
            catch (RegraNegocioException ex)
            {
                acoes.Add($"Leitura de {sensor.Codigo} recusada: {ex.Message}");
            }
        }
    }

    private decimal ProximaChuva(Sensor sensor)
    {
        var atual = estado.Valor(sensor.Id, () => sensor.UltimoValor ?? (ChuvaForte ? 10 : 0));
        var (variacao, maximo) = ChuvaForte ? (estado.Entre(-4, 8), 80m) : (estado.Entre(-2, 1.5), 15m);
        var proximo = Math.Clamp(atual + variacao, 0, maximo);
        estado.Definir(sensor.Id, proximo);
        return proximo;
    }

    /// <summary>
    /// O lixo acumula ciclo a ciclo. Com missão aberta, o robô está a caminho e o acúmulo quase para.
    /// Quando a missão termina, a boca de lobo volta limpa.
    /// </summary>
    private async Task<decimal?> ProximaObstrucaoAsync(Sensor sensor, CancellationToken ct)
    {
        var limiar = sensor.Limiar ?? 70m;
        var atual = estado.Valor(sensor.Id, () => sensor.UltimoValor ?? estado.Entre(5, 30));
        var missaoAberta = await db.Missoes.AnyAsync(m => m.SensorId == sensor.Id && m.Status != StatusMissao.Concluida, ct);

        var proximo = missaoAberta switch
        {
            true => atual + estado.Entre(0, 2),
            false when atual >= limiar => estado.Entre(5, 20),               // robô acabou de limpar
            false => atual + (ChuvaForte ? estado.Entre(4, 15) : estado.Entre(0, 5)),
        };
        proximo = Math.Clamp(proximo, 0, 100);
        estado.Definir(sensor.Id, proximo);
        return proximo;
    }

    /// <summary>A quadra só mede algo quando está operando: enche ao reter e baixa ao esvaziar.</summary>
    private async Task<decimal?> ProximoNivelAsync(Sensor sensor, CancellationToken ct)
    {
        var quadra = await db.Quadras.AsNoTracking().FirstAsync(q => q.Id == sensor.QuadraId, ct);
        var nivel = quadra.NivelAtualPercentual;

        decimal? proximo = quadra.Status switch
        {
            StatusQuadra.EmPreparacao or StatusQuadra.Retendo =>
                nivel + (ChuvaForte ? estado.Entre(6, 15) : estado.Entre(0, 3)),
            StatusQuadra.Esvaziando when nivel > 0 => nivel - estado.Entre(10, 25),
            _ => null
        };
        if (proximo is null || (proximo == 0 && nivel == 0))
            return null;
        return Math.Clamp(proximo.Value, 0, 100);
    }

    private async Task LiberarQuadrasVaziasAsync(List<string> acoes, CancellationToken ct)
    {
        var vazias = await db.Quadras.AsNoTracking()
            .Where(q => q.Status == StatusQuadra.Esvaziando && q.NivelAtualPercentual == 0)
            .Select(q => new { q.Id, q.Nome })
            .ToListAsync(ct);
        foreach (var quadra in vazias)
        {
            await quadras.AlterarStatusAsync(quadra.Id, StatusQuadra.Disponivel, ct);
            acoes.Add($"{quadra.Nome} esvaziada e liberada para uso esportivo.");
        }
    }

    private async Task AvaliarRiscoAsync(List<string> acoes, CancellationToken ct)
    {
        var bairros = await db.Bairros.AsNoTracking().OrderBy(b => b.Nome).Select(b => new { b.Id, b.Nome }).ToListAsync(ct);
        foreach (var bairro in bairros)
        {
            try
            {
                var resultado = await risco.AvaliarAsync(bairro.Id, RiscoService.HorasPadrao, ct);
                acoes.Add($"Risco de {bairro.Nome}: {resultado.Avaliacao.Nivel} " +
                          $"({resultado.Avaliacao.ChuvaPrevistaMm} mm previstos, {resultado.Avaliacao.EntupimentosAbertos} entupimento(s) aberto(s)).");
                acoes.AddRange(resultado.AcoesExecutadas.Where(a => !a.StartsWith("Risco baixo") && !a.StartsWith("Já existe")));

                if (resultado.Avaliacao.Nivel == NivelRisco.Baixo)
                    await EncerrarOperacaoDasQuadrasAsync(bairro.Id, acoes, ct);
            }
            catch (ServicoExternoException ex)
            {
                acoes.Add($"Risco de {bairro.Nome} não avaliado: {ex.Message}");
            }
        }
    }

    /// <summary>Passado o risco, a água retida é liberada devagar e quadras que nem chegaram a encher voltam ao uso.</summary>
    private async Task EncerrarOperacaoDasQuadrasAsync(Guid bairroId, List<string> acoes, CancellationToken ct)
    {
        var emOperacao = await db.Quadras.AsNoTracking()
            .Where(q => q.BairroId == bairroId && (q.Status == StatusQuadra.Retendo || q.Status == StatusQuadra.EmPreparacao))
            .Select(q => new { q.Id, q.Nome, q.Status })
            .ToListAsync(ct);

        foreach (var quadra in emOperacao)
        {
            var (novo, mensagem) = quadra.Status == StatusQuadra.Retendo
                ? (StatusQuadra.Esvaziando, $"Risco baixo: {quadra.Nome} começou a liberar a água devagar.")
                : (StatusQuadra.Disponivel, $"Risco baixo: preparação de {quadra.Nome} cancelada, quadra liberada.");
            await quadras.AlterarStatusAsync(quadra.Id, novo, ct);
            acoes.Add(mensagem);
        }
    }
}

/// <summary>Previsão fixa configurada em <c>Simulador:ChuvaPrevistaMm</c>, no lugar do Open-Meteo.</summary>
public sealed class PrevisaoSimuladaClient(IOptions<ConfiguracaoSimulador> config) : IPrevisaoTempoClient
{
    public Task<decimal> ObterChuvaPrevistaMmAsync(double latitude, double longitude, int horas, CancellationToken ct) =>
        Task.FromResult(config.Value.ChuvaPrevistaMm ?? 0m);
}

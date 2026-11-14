using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Privacidade.Eventos;
using GlobalSolutionCitizenTech.Privacidade.Logging;
using GlobalSolutionCitizenTech.Privacidade.Pii;
using GlobalSolutionCitizenTech.Privacidade.Taint;
using Microsoft.Extensions.Logging;

namespace GlobalSolutionCitizenTech.UnitTests.Privacidade;

/// <summary>Provider que guarda as linhas como chegariam ao console / OpenTelemetry.</summary>
public sealed class CaptorLogs : ILoggerProvider
{
    public List<string> Linhas { get; } = [];
    public ILogger CreateLogger(string categoryName) => new Logger(this);
    public void Dispose() { }

    private sealed class Logger(CaptorLogs captor) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var estruturado = state is IEnumerable<KeyValuePair<string, object?>> pares
                ? string.Join(";", pares.Select(p => $"{p.Key}={p.Value}"))
                : "";
            lock (captor.Linhas)
                captor.Linhas.Add($"{formatter(state, exception)} | {estruturado} | {exception}");
        }
    }
}

public class LoggerComTaintTests
{
    private readonly CaptorLogs _captor = new();
    private readonly FilaEventosSeguranca _fila = new();
    private readonly ILogger _logger;

    public LoggerComTaintTests()
    {
        var interna = LoggerFactory.Create(b => b.AddProvider(_captor).SetMinimumLevel(LogLevel.Trace));
        _logger = new FabricaLoggerComTaint(interna, new MotorPii(RegrasPadrao.Todas), _fila).CreateLogger("Demo");
    }

    private List<EventoPendente> Eventos()
    {
        var lista = new List<EventoPendente>();
        while (_fila.Leitor.TryRead(out var e)) lista.Add(e);
        return lista;
    }

    [Fact]
    public void Linha_sem_dado_pessoal_passa_intacta()
    {
        _logger.LogInformation("Quadra {Codigo} em alerta", "Q-01");
        Assert.Contains("Quadra Q-01 em alerta", _captor.Linhas.Single());
        Assert.Empty(Eventos());
    }

    [Fact]
    public void Dado_de_saude_pelo_nome_do_parametro_bloqueia_a_linha()
    {
        _logger.LogInformation("Morador com {CondicaoSaude}", "Diabetes tipo 1");

        var linha = _captor.Linhas.Single();
        Assert.StartsWith(LoggerComTaint.PrefixoBloqueio, linha);
        Assert.DoesNotContain("Diabetes", linha);
        var evento = Assert.Single(Eventos());
        Assert.Equal(TipoEventoSeguranca.VazamentoBloqueadoLog, evento.Tipo);
        Assert.Contains(CategoriaPii.Saude, evento.Categorias);
    }

    [Fact]
    public void Senha_interpolada_so_e_pega_pelo_taint()
    {
        using var _ = ContextoTaint.Iniciar("POST /login", "t1");
        ContextoTaint.Atual!.Marcar(new MotorPii(RegrasPadrao.Todas).AnalisarCampo("$.senha", "senha", "Enchente@2026!"));

#pragma warning disable CA2254
        _logger.LogWarning($"Login de ana com a senha Enchente@2026!");
#pragma warning restore CA2254

        Assert.DoesNotContain("Enchente", _captor.Linhas.Single());
        Assert.Equal("POST /login", Assert.Single(Eventos()).Origem);
    }

    [Fact]
    public void Cpf_sai_mascarado_e_a_excecao_e_omitida()
    {
        _logger.LogWarning(new InvalidOperationException("CPF 123.456.789-09 duplicado"), "Falha com {Doc}", "123.456.789-09");

        var linha = _captor.Linhas.Single();
        Assert.Contains("***.456.***-09", linha);
        Assert.DoesNotContain("123.456.789-09", linha);
        Assert.DoesNotContain("789", linha);
        Assert.Equal(CategoriaPii.Documento, Assert.Single(Eventos()).Categorias.Single());
    }
}

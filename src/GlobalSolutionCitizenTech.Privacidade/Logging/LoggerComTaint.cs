using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Privacidade.Eventos;
using GlobalSolutionCitizenTech.Privacidade.Pii;
using GlobalSolutionCitizenTech.Privacidade.Taint;
using Microsoft.Extensions.Logging;

namespace GlobalSolutionCitizenTech.Privacidade.Logging;

/// <summary>
/// Sink de log: toda <see cref="ILogger"/> da aplicação passa por aqui antes de chegar ao console, arquivo ou
/// OpenTelemetry. É o equivalente a interceptar <c>console.log(senha)</c> e <c>logger.info(dados_de_saude)</c>.
/// </summary>
/// <remarks>
/// Três verificações por linha:
/// <list type="number">
/// <item>taint: algum valor que entrou como PII nesta requisição aparece na mensagem ou na exceção?</item>
/// <item>chave: o nome do parâmetro do template (<c>{Senha}</c>, <c>{CondicaoSaude}</c>) já diz que é sensível?</item>
/// <item>valor: o texto tem CPF, JWT, e-mail... mesmo sem ter vindo da requisição?</item>
/// </list>
/// Saúde e credencial bloqueiam a linha inteira; documento e contato saem mascarados.
/// </remarks>
internal sealed class LoggerComTaint(ILogger interno, string categoria, MotorPii motor, FilaEventosSeguranca fila) : ILogger
{
    public const string PrefixoBloqueio = "[LOG BLOQUEADO pelo taint analysis";

    /// <summary>Os próprios componentes de privacidade não publicam evento: evita evento gerando evento.</summary>
    private readonly bool _publica = !categoria.StartsWith("GlobalSolutionCitizenTech.Privacidade", StringComparison.Ordinal);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => interno.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => interno.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!interno.IsEnabled(logLevel))
            return;

        var mensagem = formatter(state, exception);
        var excecao = exception?.ToString();
        var achados = Analisar(state, mensagem, excecao);

        if (achados.Count == 0)
        {
            interno.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        var categorias = achados.Select(a => a.Categoria).Distinct().ToList();
        var critico = categorias.Any(c => c is CategoriaPii.Saude or CategoriaPii.Credencial);
        var regras = string.Join(", ", achados.Select(a => a.Regra).Distinct().Order());

        // A linha sanitizada sai como texto puro: o state estruturado original carregaria o valor para o
        // provider (OpenTelemetry exporta os atributos separados da mensagem).
        var linha = critico
            ? $"{PrefixoBloqueio}: {regras} em '{categoria}', linha suprimida]"
            : MotorPii.MascararTexto(mensagem, achados) +
              (exception is null ? "" : $" [exceção {exception.GetType().Name} omitida: continha dado pessoal]");

        interno.Log(logLevel, eventId, linha, null, static (s, _) => s);

        if (_publica)
        {
            var contexto = ContextoTaint.Atual;
            var acao = critico ? "linha suprimida" : "linha mascarada";
            fila.Publicar(new EventoPendente(TipoEventoSeguranca.VazamentoBloqueadoLog, SinkSeguranca.Log, categorias,
                $"Log {logLevel} em {categoria} tentou registrar {regras} ({acao}). " +
                $"Detecção: {string.Join(", ", achados.Select(a => a.Caminho).Distinct())}.",
                contexto?.Origem ?? categoria, contexto?.TraceId, DateTimeOffset.UtcNow));
        }
    }

    private List<AchadoPii> Analisar<TState>(TState state, string mensagem, string? excecao)
    {
        var achados = new List<AchadoPii>();
        var contexto = ContextoTaint.Atual;

        if (contexto is not null)
        {
            achados.AddRange(contexto.Encontrar(mensagem));
            achados.AddRange(contexto.Encontrar(excecao));
        }

        if (state is IReadOnlyList<KeyValuePair<string, object?>> pares)
        {
            foreach (var (chave, valor) in pares)
            {
                if (chave == "{OriginalFormat}" || valor is null or bool)
                    continue;
                achados.AddRange(motor.AnalisarCampo($"log.{chave}", chave, Convert.ToString(valor)));
            }
        }

        achados.AddRange(motor.AnalisarTexto(mensagem, "log.mensagem"));
        achados.AddRange(motor.AnalisarTexto(excecao, "log.excecao"));
        return achados;
    }
}

/// <summary>Substitui o <see cref="ILoggerFactory"/>: todo logger criado (de qualquer provider) passa pela barreira.</summary>
internal sealed class FabricaLoggerComTaint(ILoggerFactory interna, MotorPii motor, FilaEventosSeguranca fila) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) =>
        new LoggerComTaint(interna.CreateLogger(categoryName), categoryName, motor, fila);

    public void AddProvider(ILoggerProvider provider) => interna.AddProvider(provider);

    public void Dispose() => interna.Dispose();
}

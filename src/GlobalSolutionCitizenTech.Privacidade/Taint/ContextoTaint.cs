using System.Collections.Concurrent;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Privacidade.Pii;

namespace GlobalSolutionCitizenTech.Privacidade.Taint;

/// <summary>
/// Contexto de taint de uma requisição: os valores que entraram como PII (source) ficam "contaminados" e
/// são procurados em toda saída (sink) até a requisição acabar.
/// </summary>
/// <remarks>
/// Vive num <see cref="AsyncLocal{T}"/>: acompanha o fluxo assíncrono da requisição até o logger, que é
/// singleton e não enxergaria um serviço scoped.
/// </remarks>
public sealed class ContextoTaint
{
    private static readonly AsyncLocal<ContextoTaint?> AtualLocal = new();

    /// <summary>Valores curtos demais casariam com qualquer texto: não entram no rastreio.</summary>
    private const int TamanhoMinimo = 6;

    private readonly ConcurrentDictionary<string, AchadoPii> _contaminados = new(StringComparer.Ordinal);

    public string Origem { get; }
    public string? TraceId { get; }

    private ContextoTaint(string origem, string? traceId)
    {
        Origem = origem;
        TraceId = traceId;
    }

    public static ContextoTaint? Atual => AtualLocal.Value;

    /// <summary>Abre o contexto da requisição; o <c>Dispose</c> fecha.</summary>
    public static IDisposable Iniciar(string origem, string? traceId)
    {
        var anterior = AtualLocal.Value;
        AtualLocal.Value = new ContextoTaint(origem, traceId);
        return new Encerramento(() => AtualLocal.Value = anterior);
    }

    public bool Vazio => _contaminados.IsEmpty;

    public IReadOnlyCollection<AchadoPii> Contaminados => _contaminados.Values.ToList();

    /// <summary>Marca os valores como contaminados (source).</summary>
    public void Marcar(IEnumerable<AchadoPii> achados)
    {
        foreach (var a in achados)
        {
            if (a.Valor.Trim().Length >= TamanhoMinimo)
                _contaminados.TryAdd(a.Valor.Trim(), a);

            // Documento e telefone também aparecem sem pontuação: "123.456.789-09" e "12345678909".
            var digitos = Mascaras.SoDigitos(a.Valor);
            if (digitos.Length >= 8 && digitos != a.Valor)
                _contaminados.TryAdd(digitos, a with { Valor = digitos });
        }
    }

    /// <summary>Procura no texto qualquer valor contaminado (sink check).</summary>
    public IReadOnlyList<AchadoPii> Encontrar(string? texto)
    {
        if (string.IsNullOrEmpty(texto) || _contaminados.IsEmpty)
            return [];

        return _contaminados
            .Where(kv => texto.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value with { Caminho = "taint:" + kv.Value.Caminho })
            .ToList();
    }

    private sealed class Encerramento(Action acao) : IDisposable
    {
        public void Dispose() => acao();
    }
}

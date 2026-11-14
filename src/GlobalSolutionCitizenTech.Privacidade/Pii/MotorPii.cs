using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Privacidade.Pii;

/// <summary>Um dado pessoal encontrado: onde, por qual regra, o trecho original e como fica mascarado.</summary>
/// <param name="Caminho">Caminho no payload (ex.: <c>$.moradores[0].cpf</c>) ou <c>texto</c>.</param>
/// <param name="Regra">Nome da regra que casou (ex.: <c>CPF</c>).</param>
/// <param name="Categoria">Categoria do dado, usada no farol.</param>
/// <param name="Valor">Trecho original. Fica só em memória, nunca é gravado nem logado.</param>
/// <param name="Mascarado">Como o trecho aparece depois da máscara.</param>
/// <param name="ValorInteiro">O campo inteiro deve ser trocado pela máscara (e não só o trecho).</param>
public sealed record AchadoPii(string Caminho, string Regra, CategoriaPii Categoria, string Valor, string Mascarado,
    bool ValorInteiro);

/// <summary>
/// Motor de parsing de PII: percorre qualquer JSON, formulário, query string ou texto livre e aplica as
/// regras (padrão + configuradas) para taguear e mascarar dados pessoais.
/// </summary>
public sealed partial class MotorPii
{
    /// <summary>Limite por regex: uma regra mal escrita (ou um payload hostil) não trava a API (ReDoS).</summary>
    private static readonly TimeSpan TempoMaximoRegex = TimeSpan.FromMilliseconds(100);

    private readonly IReadOnlyList<RegraCompilada> _regras;

    public IReadOnlyList<RegraPii> Regras { get; }

    public MotorPii(IOptions<ConfiguracaoPrivacidade> opcoes) : this(Montar(opcoes.Value)) { }

    public MotorPii(IEnumerable<RegraPii> regras)
    {
        Regras = regras.ToList();
        _regras = Regras.Select(r => new RegraCompilada(r,
            r.PadraoChave is null ? null : new Regex(r.PadraoChave, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TempoMaximoRegex),
            r.PadraoValor is null ? null : new Regex(r.PadraoValor, RegexOptions.CultureInvariant, TempoMaximoRegex))).ToList();
    }

    private static IEnumerable<RegraPii> Montar(ConfiguracaoPrivacidade c) =>
        RegrasPadrao.Todas
            .Where(r => !c.RegrasDesativadas.Contains(r.Nome, StringComparer.OrdinalIgnoreCase))
            .Concat(c.RegrasAdicionais);

    // ------------------------------------------------------------------ análise

    /// <summary>Analisa um campo: nome (chave) + valor.</summary>
    public IReadOnlyList<AchadoPii> AnalisarCampo(string caminho, string chave, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || JaMascarado(valor))
            return [];

        var chaveNormalizada = NormalizarChave(chave);
        var achados = new List<AchadoPii>();

        foreach (var r in _regras)
        {
            var chaveCasa = chaveNormalizada.Length > 0 && r.Chave is not null && Casa(r.Chave, chaveNormalizada);
            var trechos = new List<string>();

            if (r.Valor is not null && (chaveCasa || !r.Regra.ValorExigeChave))
            {
                foreach (Match m in Encontrar(r.Valor, valor))
                    if (Validar(r.Regra, m.Value))
                        trechos.Add(m.Value);
            }

            if (trechos.Count > 0)
            {
                var inteiro = r.Regra.MascararValorInteiro;
                achados.AddRange(inteiro
                    ? [Novo(caminho, r.Regra, valor, true)]
                    : trechos.Distinct().Select(t => Novo(caminho, r.Regra, t, false)));
            }
            else if (chaveCasa)
            {
                // O nome do campo já diz o que é (ex.: "laudo", "clientSecret"): o valor todo é PII.
                achados.Add(Novo(caminho, r.Regra, valor, true));
            }
        }

        return Consolidar(achados);
    }

    /// <summary>Analisa texto livre (mensagem de log, mensagem de erro): só as regras de valor.</summary>
    public IReadOnlyList<AchadoPii> AnalisarTexto(string? texto, string caminho = "texto") =>
        AnalisarCampo(caminho, "", texto);

    /// <summary>Percorre o JSON inteiro (objetos, arrays, aninhados) analisando cada valor com o nome do seu campo.</summary>
    public IReadOnlyList<AchadoPii> AnalisarJson(JsonNode? raiz)
    {
        var achados = new List<AchadoPii>();
        Percorrer(raiz, "$", "", (caminho, chave, folha) =>
            achados.AddRange(AnalisarCampo(caminho, chave, ValorDaFolha(folha))));
        return achados;
    }

    // ------------------------------------------------------------------ máscara

    /// <summary>Troca, no texto, cada trecho achado pela sua máscara.</summary>
    public static string MascararTexto(string texto, IEnumerable<AchadoPii> achados)
    {
        foreach (var a in achados.OrderByDescending(a => a.Valor.Length))
        {
            if (a.ValorInteiro && texto == a.Valor)
                return a.Mascarado;
            texto = texto.Replace(a.Valor, a.Mascarado, StringComparison.Ordinal);
        }
        return texto;
    }

    /// <summary>Mascara o JSON no lugar. Também esconde valores que o contexto de taint marcou nesta requisição.</summary>
    /// <returns>Os dados pessoais encontrados (já mascarados no JSON).</returns>
    public IReadOnlyList<AchadoPii> MascararJson(JsonNode? raiz, Func<string, IReadOnlyList<AchadoPii>>? taint = null)
    {
        var todos = new List<AchadoPii>();
        Percorrer(raiz, "$", "", (caminho, chave, folha) =>
        {
            var valor = ValorDaFolha(folha);
            if (string.IsNullOrEmpty(valor))
                return;

            var achados = AnalisarCampo(caminho, chave, valor).Concat(taint?.Invoke(valor) ?? []).ToList();
            if (achados.Count == 0)
                return;

            todos.AddRange(achados);
            var inteiro = achados.FirstOrDefault(a => a.ValorInteiro && a.Valor == valor);
            var novo = inteiro?.Mascarado ?? MascararTexto(valor, achados);
            folha.ReplaceWith(JsonValue.Create(novo));
        });
        return todos;
    }

    public AchadoPii Novo(string caminho, RegraPii regra, string valor, bool inteiro) =>
        new(caminho, regra.Nome, regra.Categoria, valor, Mascarar(regra, valor), inteiro);

    public static string Mascarar(RegraPii regra, string valor) => regra.Mascara switch
    {
        TipoMascara.Cpf => Mascaras.Cpf(valor),
        TipoMascara.Telefone => Mascaras.Telefone(valor),
        TipoMascara.Email => Mascaras.Email(valor),
        TipoMascara.Parcial => Mascaras.Parcial(valor),
        _ => Mascaras.Total(valor, regra.Rotulo)
    };

    // ------------------------------------------------------------------ apoio

    public static string NormalizarChave(string chave) => NaoAlfanumerico().Replace(chave.ToLowerInvariant(), "");

    private static bool Validar(RegraPii regra, string valor) => regra.Validador?.ToLowerInvariant() switch
    {
        null => true,
        "cpf" => Cpf.Valido(valor),
        _ => true
    };

    /// <summary>Uma regra de valor e uma de chave podem achar o mesmo trecho: fica o achado mais específico.</summary>
    private static IReadOnlyList<AchadoPii> Consolidar(List<AchadoPii> achados) =>
        achados
            .GroupBy(a => (a.Caminho, a.Valor))
            .Select(g => g.OrderBy(a => a.ValorInteiro ? 1 : 0).First())
            .ToList();

    private static bool Casa(Regex regex, string texto)
    {
        try { return regex.IsMatch(texto); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    private static IEnumerable<Match> Encontrar(Regex regex, string texto)
    {
        try { return regex.Matches(texto).ToList(); }
        catch (RegexMatchTimeoutException) { return []; }
    }

    /// <summary>Texto ou número; booleano e nulo não carregam dado pessoal.</summary>
    private static string? ValorDaFolha(JsonNode folha) =>
        folha is JsonValue v && v.GetValueKind() is System.Text.Json.JsonValueKind.String or System.Text.Json.JsonValueKind.Number
            ? v.TryGetValue<string>(out var s) ? s : v.ToJsonString()
            : null;

    /// <summary>Valor que já saiu de uma máscara (<c>***.456.***-09</c>, <c>[credencial protegida]</c>) não é PII.</summary>
    public static bool JaMascarado(string valor) =>
        valor.Contains("***", StringComparison.Ordinal) || (valor.StartsWith('[') && valor.EndsWith(']'));

    private static void Percorrer(JsonNode? no, string caminho, string chave, Action<string, string, JsonNode> folha)
    {
        switch (no)
        {
            case JsonObject obj:
                foreach (var (nome, filho) in obj.ToList())
                    Percorrer(filho, $"{caminho}.{nome}", nome, folha);
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                    Percorrer(arr[i], $"{caminho}[{i}]", chave, folha);   // item de array herda o nome do campo
                break;
            case JsonValue valor:
                folha(caminho, chave, valor);
                break;
        }
    }

    private sealed record RegraCompilada(RegraPii Regra, Regex? Chave, Regex? Valor);

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NaoAlfanumerico();
}

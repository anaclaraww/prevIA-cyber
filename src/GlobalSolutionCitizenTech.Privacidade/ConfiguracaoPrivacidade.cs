using GlobalSolutionCitizenTech.Privacidade.Pii;

namespace GlobalSolutionCitizenTech.Privacidade;

/// <summary>Seção <c>Privacidade</c> do appsettings.</summary>
public sealed class ConfiguracaoPrivacidade
{
    public const string Secao = "Privacidade";

    /// <summary>Regras extras do motor de parsing, somadas às <see cref="RegrasPadrao"/>.</summary>
    public List<RegraPii> RegrasAdicionais { get; set; } = [];

    /// <summary>Nomes de regras padrão a desligar (ex.: <c>Email</c>).</summary>
    public List<string> RegrasDesativadas { get; set; } = [];

    /// <summary>Chave AES-256 em Base64 (32 bytes) para cifrar telefone e condição de saúde.</summary>
    public string ChaveCriptografia { get; set; } = "";

    /// <summary>Chave do HMAC-SHA256 que gera o hash do CPF (busca de duplicidade).</summary>
    public string ChaveHash { get; set; } = "";

    /// <summary>Liga os endpoints de demonstração de vazamento (código "vulnerável" de propósito).</summary>
    public bool CenariosDemonstracao { get; set; }

    public ConfiguracaoIa Ia { get; set; } = new();
}

/// <summary>Tradução dos eventos via LLM (Claude).</summary>
public sealed class ConfiguracaoIa
{
    /// <summary>Chave da API da Anthropic. Vazia = usa os textos offline. Nunca vai para o repositório.</summary>
    public string? ApiKey { get; set; }

    public string Modelo { get; set; } = "claude-opus-5-5";

    public int TimeoutSegundos { get; set; } = 30;
}

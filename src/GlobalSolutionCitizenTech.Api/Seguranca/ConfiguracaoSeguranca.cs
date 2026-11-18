namespace GlobalSolutionCitizenTech.Api.Seguranca;

/// <summary>Seção <c>Seguranca</c> do appsettings: o endurecimento da API para rodar fora do localhost.</summary>
public class ConfiguracaoSeguranca
{
    public const string Secao = "Seguranca";

    /// <summary>
    /// Publica <c>/swagger</c>. Desligado por padrão: a documentação revela todas as rotas e esquemas. Ligado só no
    /// appsettings.Development.json (a versão pública da documentação fica no GitHub Pages, sem servidor por trás).
    /// </summary>
    public bool ExporSwagger { get; set; }

    /// <summary>
    /// Maior corpo de requisição aceito, em bytes. A API só recebe JSON pequeno; o mesmo valor é o teto que o motor
    /// de PII consegue varrer, então nada maior chega ao controller sem passar pelo taint analysis.
    /// </summary>
    public long TamanhoMaximoCorpoBytes { get; set; } = 1024 * 1024;

    public ConfiguracaoRateLimit RateLimit { get; set; } = new();

    /// <summary>
    /// IPs ou redes (CIDR) dos proxies reversos confiáveis (gateway, ALB, nginx). Só deles a API aceita
    /// <c>X-Forwarded-For</c>/<c>X-Forwarded-Proto</c>. Vazio = ninguém, e o IP do rate limit é o da conexão.
    /// </summary>
    public List<string> ProxiesConfiaveis { get; set; } = [];
}

/// <summary>Limites por IP, em janela fixa de 1 minuto.</summary>
public class ConfiguracaoRateLimit
{
    /// <summary>Tentativas de login (<c>POST /api/v1/auth/token</c>) por IP por minuto: freia força bruta.</summary>
    public int LoginPorMinuto { get; set; } = 5;

    /// <summary>Requisições por IP por minuto, somando todas as rotas (exceto <c>/health</c>).</summary>
    public int RequisicoesPorMinuto { get; set; } = 120;
}

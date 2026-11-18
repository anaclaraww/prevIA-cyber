using System.Net;
using System.Threading.RateLimiting;
using GlobalSolutionCitizenTech.Api.Erros;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Api.Seguranca;

/// <summary>Endurecimento da API para rodar exposta: rate limit, cabeçalhos, teto de corpo, proxy confiável e HSTS.</summary>
public static class SegurancaExtensions
{
    /// <summary>Política de rate limit do login, aplicada com <c>[EnableRateLimiting]</c> no <c>AuthController</c>.</summary>
    public const string PoliticaLogin = "login";

    public static IServiceCollection AddSegurancaApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConfiguracaoSeguranca>(configuration.GetSection(ConfiguracaoSeguranca.Secao));

        // Sem o cabeçalho "Server: Kestrel" (não entrega a tecnologia de graça) e com o mesmo teto de corpo.
        services.AddOptions<KestrelServerOptions>().Configure<IOptions<ConfiguracaoSeguranca>>((kestrel, seg) =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = seg.Value.TamanhoMaximoCorpoBytes;
        });

        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ConfiguracaoSeguranca>>((o, seg) =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
            foreach (var item in seg.Value.ProxiesConfiaveis)
            {
                if (item.Contains('/'))
                    o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(item));
                else
                    o.KnownProxies.Add(IPAddress.Parse(item));
            }
        });

        services.AddHsts(o =>
        {
            o.MaxAge = TimeSpan.FromDays(365);
            o.IncludeSubDomains = true;
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (contexto, ct) =>
            {
                var http = contexto.HttpContext;
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString();

                await http.Response.WriteAsJsonAsync(new ErroResponse(false, CodigosErro.MuitasRequisicoes,
                    "Muitas requisições em pouco tempo. Aguarde e tente novamente.", null, http.TraceIdentifier), ct);
            };

            // Limite geral por IP. /health fica de fora: é o probe do orquestrador, não um cliente.
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                http.Request.Path.StartsWithSegments("/health")
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ =>
                        JanelaDeUmMinuto(Limites(http).RequisicoesPorMinuto)));

            o.AddPolicy(PoliticaLogin, http =>
                RateLimitPartition.GetFixedWindowLimiter(Ip(http), _ => JanelaDeUmMinuto(Limites(http).LoginPorMinuto)));
        });

        return services;
    }

    /// <summary>Encaixa as camadas no início do pipeline, antes da privacidade e do tratamento de erros.</summary>
    public static void UseSegurancaApi(this WebApplication app)
    {
        var seg = app.Services.GetRequiredService<IOptions<ConfiguracaoSeguranca>>().Value;

        // Primeiro: o IP real do cliente precisa estar no lugar antes do rate limit usar.
        if (seg.ProxiesConfiaveis.Count > 0)
            app.UseForwardedHeaders();

        // HSTS só sai em resposta HTTPS (atrás do proxy, via X-Forwarded-Proto); em dev não faz sentido.
        if (!app.Environment.IsDevelopment())
            app.UseHsts();

        app.UseMiddleware<CabecalhosSegurancaMiddleware>();
        app.UseRateLimiter();
    }

    private static ConfiguracaoRateLimit Limites(HttpContext http) =>
        http.RequestServices.GetRequiredService<IOptions<ConfiguracaoSeguranca>>().Value.RateLimit;

    private static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

    private static FixedWindowRateLimiterOptions JanelaDeUmMinuto(int permitidas) => new()
    {
        PermitLimit = permitidas,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };
}

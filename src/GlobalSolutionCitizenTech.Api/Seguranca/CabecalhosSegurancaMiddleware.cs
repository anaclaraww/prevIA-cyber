using GlobalSolutionCitizenTech.Api.Erros;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Api.Seguranca;

/// <summary>
/// Camada mais externa da API: cabeçalhos de segurança em toda resposta (inclusive erros e 429) e o teto do corpo
/// da requisição.
/// </summary>
/// <remarks>
/// Cabeçalhos seguindo o OWASP Secure Headers Project e o REST Security Cheat Sheet. <c>X-XSS-Protection</c> fica
/// de fora de propósito: está descontinuado e a recomendação atual é não enviá-lo.
/// </remarks>
public sealed class CabecalhosSegurancaMiddleware(RequestDelegate next, IOptions<ConfiguracaoSeguranca> opcoes)
{
    /// <summary>Painel e respostas JSON: só recursos da própria origem, sem script/estilo inline, sem iframe.</summary>
    public const string Csp =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; " +
        "object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        var limite = opcoes.Value.TamanhoMaximoCorpoBytes;

        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            h["Cross-Origin-Resource-Policy"] = "same-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

            // O Swagger UI depende de script inline; ele só existe com Seguranca:ExporSwagger ligado (dev).
            if (!context.Request.Path.StartsWithSegments("/swagger"))
                h.ContentSecurityPolicy = Csp;

            // Respostas da API carregam dado pessoal (mascarado ou não): nada de cache em proxy ou navegador.
            if (context.Request.Path.StartsWithSegments("/api"))
                h.CacheControl = "no-store";

            return Task.CompletedTask;
        });

        // Teto do corpo: com Content-Length, recusa antes de ler; sem ele (chunked), o servidor corta na leitura.
        var recurso = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (recurso is { IsReadOnly: false })
            recurso.MaxRequestBodySize = limite;

        if (context.Request.ContentLength > limite)
        {
            await PayloadGrandeAsync(context, limite);
            return;
        }

        try
        {
            await next(context);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                                                 && !context.Response.HasStarted)
        {
            await PayloadGrandeAsync(context, limite);
        }
    }

    private static Task PayloadGrandeAsync(HttpContext context, long limite)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return context.Response.WriteAsJsonAsync(new ErroResponse(false, CodigosErro.PayloadGrande,
            $"O corpo da requisição passa do limite de {limite / 1024} KB.", null, context.TraceIdentifier));
    }
}

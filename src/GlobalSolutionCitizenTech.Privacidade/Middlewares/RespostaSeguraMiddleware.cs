using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Privacidade.Eventos;
using GlobalSolutionCitizenTech.Privacidade.Pii;
using GlobalSolutionCitizenTech.Privacidade.Taint;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GlobalSolutionCitizenTech.Privacidade.Middlewares;

/// <summary>
/// Sink HTTP do taint analysis, a camada mais externa da API: abre o contexto de taint da requisição e,
/// na volta, inspeciona o JSON antes de ele sair.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Sem autenticação + dado pessoal na resposta -> <c>403 SENSITIVE_DATA_BLOCKED</c>, nada do corpo sai.</item>
/// <item>Autenticado + dado pessoal -> sai mascarado (inclusive mensagens de erro que ecoam o payload).</item>
/// <item>Endpoint com <see cref="PermiteDadosSensiveisAttribute"/> -> sai como está (quem acessa é decidido pelo <c>[Authorize]</c>).</item>
/// </list>
/// Fica por fora do tratamento de erros: o envelope de erro também passa por aqui.
/// </remarks>
public sealed class RespostaSeguraMiddleware(RequestDelegate next, MotorPii motor, FilaEventosSeguranca fila,
    ILogger<RespostaSeguraMiddleware> logger)
{
    public const string CodigoBloqueio = "SENSITIVE_DATA_BLOCKED";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var origem = $"{context.Request.Method} {context.Request.Path}";
        using var _ = ContextoTaint.Iniciar(origem, context.TraceIdentifier);

        var corpoOriginal = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
            await InspecionarAsync(context, buffer, corpoOriginal, origem);
        }
        finally
        {
            context.Response.Body = corpoOriginal;
        }
    }

    private async Task InspecionarAsync(HttpContext context, MemoryStream buffer, Stream destino, string origem)
    {
        var autenticado = context.User.Identity?.IsAuthenticated == true;
        var liberado = context.GetEndpoint()?.Metadata.GetMetadata<PermiteDadosSensiveisAttribute>() is not null;
        var ehJson = context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

        if (liberado || !ehJson || buffer.Length == 0)
        {
            await Copiar(buffer, destino, context);
            return;
        }

        JsonNode? raiz;
        try
        {
            buffer.Position = 0;
            raiz = await JsonNode.ParseAsync(buffer);
        }
        catch (JsonException)
        {
            await Copiar(buffer, destino, context);
            return;
        }

        var taint = ContextoTaint.Atual;
        var achados = motor.MascararJson(raiz, valor => taint?.Encontrar(valor) ?? []);
        if (achados.Count == 0)
        {
            await Copiar(buffer, destino, context);
            return;
        }

        var categorias = achados.Select(a => a.Categoria).Distinct().ToList();
        var campos = string.Join(", ", achados.Select(a => $"{a.Caminho}[{a.Regra}]").Distinct());
        byte[] saida;

        if (!autenticado)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            saida = JsonSerializer.SerializeToUtf8Bytes(new
            {
                success = false,
                errorCode = CodigoBloqueio,
                message = "Esta resposta continha dados pessoais e foi bloqueada para quem não está autenticado.",
                details = (object?)null,
                traceId = context.TraceIdentifier
            }, Json);

            logger.LogWarning("Resposta anônima de {Origem} bloqueada: {Campos}", origem, campos);
            Publicar(TipoEventoSeguranca.VazamentoBloqueadoResposta, categorias,
                $"Resposta HTTP {origem} sem autenticação continha {campos}; bloqueada com 403 {CodigoBloqueio}.",
                origem, context.TraceIdentifier);
        }
        else
        {
            saida = Encoding.UTF8.GetBytes(raiz!.ToJsonString(Json));
            logger.LogWarning("Resposta de {Origem} mascarada antes de sair: {Campos}", origem, campos);
            Publicar(TipoEventoSeguranca.VazamentoMascaradoResposta, categorias,
                $"Resposta HTTP {origem} ({context.Response.StatusCode}) ia expor {campos}; mascarada antes de sair.",
                origem, context.TraceIdentifier);
        }

        context.Response.ContentLength = saida.Length;
        await destino.WriteAsync(saida, context.RequestAborted);
    }

    private void Publicar(TipoEventoSeguranca tipo, IReadOnlyCollection<CategoriaPii> categorias, string descricao,
        string origem, string traceId) =>
        fila.Publicar(new EventoPendente(tipo, SinkSeguranca.RespostaHttp, categorias, descricao, origem, traceId,
            DateTimeOffset.UtcNow));

    private static async Task Copiar(MemoryStream buffer, Stream destino, HttpContext context)
    {
        buffer.Position = 0;
        if (context.Response.ContentLength is not null)
            context.Response.ContentLength = buffer.Length;
        await buffer.CopyToAsync(destino, context.RequestAborted);
    }
}

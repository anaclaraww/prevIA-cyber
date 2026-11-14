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
/// Source do taint analysis: lê o que chega (JSON, formulário, query string), roda o motor de PII e marca os
/// valores encontrados como contaminados para o resto da requisição.
/// </summary>
/// <remarks>
/// O corpo não é alterado: o serviço precisa do CPF real para validar o dígito e gerar o hash. A proteção
/// acontece na gravação (máscara + cifra) e em cada sink (log, resposta).
/// </remarks>
public sealed class FontePiiMiddleware(RequestDelegate next, MotorPii motor, FilaEventosSeguranca fila,
    ILogger<FontePiiMiddleware> logger)
{
    /// <summary>Corpo maior que isso não é analisado (e não é aceito por nenhum endpoint da API).</summary>
    private const int TamanhoMaximoCorpo = 1024 * 1024;

    public async Task InvokeAsync(HttpContext context)
    {
        var contexto = ContextoTaint.Atual;
        if (contexto is null)
        {
            await next(context);
            return;
        }

        var achados = new List<AchadoPii>();

        foreach (var (chave, valores) in context.Request.Query)
            foreach (var valor in valores)
                achados.AddRange(motor.AnalisarCampo($"query.{chave}", chave, valor));

        if (context.Request.HasJsonContentType())
            achados.AddRange(await LerJsonAsync(context.Request));
        else if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            foreach (var (chave, valores) in form)
                foreach (var valor in valores)
                    achados.AddRange(motor.AnalisarCampo($"form.{chave}", chave, valor));
        }

        if (achados.Count > 0)
        {
            contexto.Marcar(achados);

            var campos = string.Join(", ", achados
                .GroupBy(a => a.Caminho)
                .Select(g => $"{g.Key}[{string.Join("/", g.Select(a => a.Regra).Distinct())}]"));

            // Só caminho, regra e máscara: o log mostra que o dado passou, nunca o dado.
            logger.LogInformation("PII na entrada de {Origem}: {Campos}", contexto.Origem,
                string.Join(", ", achados.Select(a => $"{a.Caminho} -> {a.Mascarado}")));

            // Login (clientSecret) não é dado do cidadão: não entra no painel.
            if (context.GetEndpoint()?.Metadata.GetMetadata<PermiteDadosSensiveisAttribute>() is null)
                fila.Publicar(new EventoPendente(TipoEventoSeguranca.PiiProtegida, SinkSeguranca.Entrada,
                    achados.Select(a => a.Categoria).Distinct().ToList(),
                    $"Entrada {contexto.Origem}: {campos} tagueados como PII e protegidos antes do banco e dos logs.",
                    contexto.Origem, contexto.TraceId, DateTimeOffset.UtcNow));
        }

        await next(context);
    }

    private async Task<IReadOnlyList<AchadoPii>> LerJsonAsync(HttpRequest request)
    {
        if (request.ContentLength is > TamanhoMaximoCorpo)
            return [];

        request.EnableBuffering(bufferThreshold: 64 * 1024, bufferLimit: TamanhoMaximoCorpo);
        try
        {
            var raiz = await JsonNode.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            return motor.AnalisarJson(raiz);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // JSON malformado ou texto que não é UTF-8 válido: o model binding devolve o 400; aqui não há o
            // que rastrear, e o motor não pode derrubar a requisição com 500.
            return [];
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}

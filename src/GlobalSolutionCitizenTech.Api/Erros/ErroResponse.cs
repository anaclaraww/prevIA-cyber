namespace GlobalSolutionCitizenTech.Api.Erros;

/// <summary>Envelope único de erro da API.</summary>
/// <param name="Success">Sempre <c>false</c>.</param>
/// <param name="ErrorCode">Código estável para o cliente tratar o erro (ex.: <c>VALIDATION_ERROR</c>).</param>
/// <param name="Message">Mensagem legível.</param>
/// <param name="Details">Detalhes (ex.: erros por campo na validação).</param>
/// <param name="TraceId">Identificador da requisição, para rastrear nos logs.</param>
public record ErroResponse(bool Success, string ErrorCode, string Message, object? Details, string TraceId);

public static class CodigosErro
{
    public const string Validacao = "VALIDATION_ERROR";
    public const string NaoAutenticado = "UNAUTHORIZED_ACCESS";
    public const string SemPermissao = "FORBIDDEN";
    public const string NaoEncontrado = "RESOURCE_NOT_FOUND";
    public const string RegraNegocio = "BUSINESS_RULE_VIOLATION";
    public const string Duplicado = "DUPLICATE_RECORD";
    public const string ApiExterna = "EXTERNAL_API_ERROR";
    public const string Interno = "INTERNAL_SERVER_ERROR";
    public const string MuitasRequisicoes = "TOO_MANY_REQUESTS";
    public const string PayloadGrande = "PAYLOAD_TOO_LARGE";
}

using GlobalSolutionCitizenTech.Api.Autenticacao;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GlobalSolutionCitizenTech.Api.Erros;

/// <summary>
/// Converte exceções em respostas HTTP com o envelope <see cref="ErroResponse"/>.
/// Controllers e serviços não têm try/catch: as exceções sobem até aqui.
/// </summary>
public class TratamentoErrosMiddleware(RequestDelegate next, ILogger<TratamentoErrosMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, codigo, mensagem) = Mapear(ex);

            if (status >= 500)
                logger.LogError(ex, "Erro {Codigo} em {Metodo} {Caminho}", codigo, context.Request.Method, context.Request.Path);
            else
                logger.LogWarning("Erro {Codigo} em {Metodo} {Caminho}: {Mensagem}", codigo, context.Request.Method, context.Request.Path, ex.Message);

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                new ErroResponse(false, codigo, mensagem, status >= 500 ? null : ex.Message, context.TraceIdentifier));
        }
    }

    private static (int Status, string Codigo, string Mensagem) Mapear(Exception ex) => ex switch
    {
        CredenciaisInvalidasException => (StatusCodes.Status401Unauthorized, CodigosErro.NaoAutenticado, "Credenciais inválidas."),
        RecursoNaoEncontradoException => (StatusCodes.Status404NotFound, CodigosErro.NaoEncontrado, "Recurso não encontrado."),
        DadosInvalidosException => (StatusCodes.Status400BadRequest, CodigosErro.Validacao, "Os dados enviados são inválidos."),
        RegraNegocioException => (StatusCodes.Status409Conflict, CodigosErro.RegraNegocio, "A operação viola uma regra de negócio."),
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
            (StatusCodes.Status409Conflict, CodigosErro.Duplicado, "Registro duplicado."),
        ServicoExternoException => (StatusCodes.Status502BadGateway, CodigosErro.ApiExterna, "Falha ao consultar um serviço externo. Tente novamente."),
        _ => (StatusCodes.Status500InternalServerError, CodigosErro.Interno, "Erro interno. Tente novamente mais tarde.")
    };
}

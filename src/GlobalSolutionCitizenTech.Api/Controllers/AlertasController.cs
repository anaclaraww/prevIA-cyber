using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Api.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Alertas para a Defesa Civil e a população.</summary>
[ApiController]
[Route("api/v1/alertas")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Alertas")]
public class AlertasController(IAlertaService alertas) : ControllerBase
{
    /// <summary>Lista os alertas, do mais grave e recente para o menos, com filtro por bairro e situação.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet]
    public Task<IReadOnlyList<AlertaResponse>> Listar([FromQuery] Guid? bairroId, [FromQuery] bool? ativo,
        CancellationToken ct) => alertas.ListarAsync(bairroId, ativo, ct);

    /// <summary>Encerra um alerta ativo.</summary>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPatch("{id:guid}/encerrar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public Task<AlertaResponse> Encerrar(Guid id, CancellationToken ct) => alertas.EncerrarAsync(id, ct);
}

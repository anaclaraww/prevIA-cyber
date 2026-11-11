using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Api.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Missões dos robôs (desobstrução e inspeção).</summary>
[ApiController]
[Route("api/v1/missoes")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Missões")]
public class MissoesController(IMissaoService missoes) : ControllerBase
{
    /// <summary>Fila de missões, da mais prioritária para a menos, com filtro por status e bairro.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet]
    public Task<IReadOnlyList<MissaoResponse>> Listar([FromQuery] StatusMissao? status, [FromQuery] Guid? bairroId,
        CancellationToken ct) => missoes.ListarAsync(status, bairroId, ct);

    /// <summary>Obtém uma missão.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<MissaoResponse> Obter(Guid id, CancellationToken ct) => missoes.ObterAsync(id, ct);

    /// <summary>Abre uma missão manual (ex.: inspeção preventiva) e despacha um robô compatível, se houver.</summary>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MissaoResponse>> Criar(CriarMissaoRequest request, CancellationToken ct)
    {
        var criada = await missoes.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criada.Id }, criada);
    }

    /// <summary>Conclui a missão. O robô consome 15% de bateria e já assume a próxima missão pendente do bairro.</summary>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPatch("{id:guid}/concluir")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public Task<MissaoConcluidaResponse> Concluir(Guid id, CancellationToken ct) => missoes.ConcluirAsync(id, ct);
}

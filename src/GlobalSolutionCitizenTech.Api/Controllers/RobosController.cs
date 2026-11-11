using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Api.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Robôs de drenagem (inspetores e desentupidores).</summary>
[ApiController]
[Route("api/v1/robos")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Robôs")]
public class RobosController(IRoboService robos) : ControllerBase
{
    /// <summary>Lista os robôs, com filtro opcional por bairro e status.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet]
    public Task<IReadOnlyList<RoboResponse>> Listar([FromQuery] Guid? bairroId, [FromQuery] StatusRobo? status,
        CancellationToken ct) => robos.ListarAsync(bairroId, status, ct);

    /// <summary>Obtém um robô.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<RoboResponse> Obter(Guid id, CancellationToken ct) => robos.ObterAsync(id, ct);

    /// <summary>Cadastra um robô. Se houver missão pendente compatível no bairro, ele já a assume.</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoboResponse>> Criar(CriarRoboRequest request, CancellationToken ct)
    {
        var criado = await robos.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    /// <summary>Recarrega a bateria (100%). O robô já assume a próxima missão pendente do bairro, se houver.</summary>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPost("{id:guid}/recarregar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public Task<RoboResponse> Recarregar(Guid id, CancellationToken ct) => robos.RecarregarAsync(id, ct);

    /// <summary>Envia o robô para manutenção (não pode estar em missão).</summary>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPost("{id:guid}/manutencao")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public Task<RoboResponse> EnviarParaManutencao(Guid id, CancellationToken ct) => robos.EnviarParaManutencaoAsync(id, ct);

    /// <summary>Remove um robô sem histórico de missões.</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        await robos.RemoverAsync(id, ct);
        return NoContent();
    }
}

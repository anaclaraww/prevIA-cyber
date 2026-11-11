using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Api.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Quadras inteligentes que viram reservatório em chuva forte.</summary>
[ApiController]
[Route("api/v1/quadras")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Quadras inteligentes")]
public class QuadrasController(IQuadraService quadras) : ControllerBase
{
    /// <summary>Lista as quadras, com filtro opcional por bairro e status.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet]
    public Task<IReadOnlyList<QuadraResponse>> Listar([FromQuery] Guid? bairroId, [FromQuery] StatusQuadra? status,
        CancellationToken ct) => quadras.ListarAsync(bairroId, status, ct);

    /// <summary>Obtém uma quadra, incluindo os próximos status permitidos.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<QuadraResponse> Obter(Guid id, CancellationToken ct) => quadras.ObterAsync(id, ct);

    /// <summary>Cadastra uma quadra. O logradouro é preenchido pelo CEP (ViaCEP).</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<QuadraResponse>> Criar(CriarQuadraRequest request, CancellationToken ct)
    {
        var criada = await quadras.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criada.Id }, criada);
    }

    /// <summary>Atualiza nome, número e capacidade da quadra.</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<QuadraResponse> Atualizar(Guid id, AtualizarQuadraRequest request, CancellationToken ct) =>
        quadras.AtualizarAsync(id, request, ct);

    /// <summary>Altera o status da quadra seguindo a máquina de estados.</summary>
    /// <remarks>
    /// Transições permitidas:
    ///
    /// - `Disponivel` → `EmPreparacao`, `Manutencao`
    /// - `EmPreparacao` → `Retendo`, `Disponivel`
    /// - `Retendo` → `Esvaziando`
    /// - `Esvaziando` → `Disponivel`
    /// - `Manutencao` → `Disponivel`
    ///
    /// Qualquer outra devolve `409 BUSINESS_RULE_VIOLATION`.
    /// </remarks>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public Task<QuadraResponse> AlterarStatus(Guid id, AlterarStatusQuadraRequest request, CancellationToken ct) =>
        quadras.AlterarStatusAsync(id, request.Status!.Value, ct);

    /// <summary>Remove uma quadra fora de operação e sem sensores vinculados.</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        await quadras.RemoverAsync(id, ct);
        return NoContent();
    }
}

using GlobalSolutionCitizenTech.Api.Autenticacao;
using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Privacidade.Taint;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Moradores vulneráveis: quem precisa de prioridade na evacuação. Dados pessoais protegidos.</summary>
[ApiController]
[Route("api/v1/moradores")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Moradores vulneráveis")]
public class MoradoresController(IMoradorService moradores) : ControllerBase
{
    /// <summary>Cadastra um morador vulnerável.</summary>
    /// <remarks>
    /// O CPF é gravado mascarado (`***.456.***-09`) + hash HMAC; o RG só mascarado; telefone e condição de
    /// saúde cifrados com AES-256-GCM. A resposta nunca devolve esses dados em claro.
    /// </remarks>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MoradorResponse>> Criar(CriarMoradorRequest request, CancellationToken ct)
    {
        var criado = await moradores.CriarAsync(request, ct);
        return Created($"/api/v1/moradores/{criado.Id}", criado);
    }

    /// <summary>Lista os moradores (dados mascarados), opcionalmente por bairro.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet]
    public Task<IReadOnlyList<MoradorResponse>> Listar([FromQuery] Guid? bairroId, CancellationToken ct) =>
        moradores.ListarAsync(bairroId, ct);

    /// <summary>Contato de emergência: telefone e condição de saúde em claro, para a equipe de resgate.</summary>
    /// <remarks>Único endpoint que devolve dado sensível, e só para quem opera a evacuação.</remarks>
    [Authorize(Roles = Acesso.Operacao)]
    [PermiteDadosSensiveis("Equipe de resgate precisa ligar para o morador e saber da condição de saúde na evacuação.")]
    [HttpGet("{id:guid}/contato-emergencia")]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<ContatoEmergenciaResponse> ContatoEmergencia(Guid id, CancellationToken ct) =>
        moradores.ContatoEmergenciaAsync(id, ct);
}

using GlobalSolutionCitizenTech.Api.Autenticacao;
using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Privacidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Painel de privacidade: indicador Risco Cidadão, farol e eventos traduzidos pela IA.</summary>
[ApiController]
[Route("api/v1/seguranca")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Segurança e privacidade")]
public class SegurancaController(IPainelSegurancaService painel) : ControllerBase
{
    /// <summary>Indicador Risco Cidadão (0 a 100) da última hora, farol e eventos.</summary>
    /// <remarks>
    /// O farol é calculado por regra (não pela IA). A IA só escreve `mensagemCidadao`, em linguagem simples.
    /// </remarks>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("painel")]
    public Task<PainelRiscoCidadaoResponse> Painel(CancellationToken ct) => painel.ObterAsync(ct);
}

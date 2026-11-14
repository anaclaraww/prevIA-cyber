using GlobalSolutionCitizenTech.Api.Autenticacao;
using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Privacidade.Taint;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Emissão de token JWT.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[Tags("Autenticação")]
[AllowAnonymous]
public class AuthController(IEmissorToken emissor) : ControllerBase
{
    /// <summary>Troca as credenciais do cliente por um token JWT.</summary>
    /// <remarks>
    /// Use o token nas demais chamadas: `Authorization: Bearer {accessToken}`.
    /// No Swagger, clique em **Authorize** e cole só o token.
    ///
    /// | Perfil | Pode |
    /// | --- | --- |
    /// | `Sensor` | enviar leituras |
    /// | `Operador` | consultar tudo, avaliar risco, operar quadras, missões, robôs e alertas |
    /// | `Admin` | tudo, inclusive cadastros e o simulador |
    /// </remarks>
    [HttpPost("token")]
    [PermiteDadosSensiveis("O token JWT é a própria resposta do login.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
    public TokenResponse GerarToken(TokenRequest request) => emissor.Emitir(request);
}

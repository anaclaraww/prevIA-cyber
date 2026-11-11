using GlobalSolutionCitizenTech.Api.Autenticacao;
using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Simulacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Simulador de sensores e robôs, para demonstrar a cidade em funcionamento.</summary>
[ApiController]
[Route("api/v1/simulador")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Simulador")]
public class SimuladorController(ISimuladorSensores simulador) : ControllerBase
{
    /// <summary>Configuração e estado do simulador.</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpGet]
    public StatusSimuladorResponse Status() => simulador.Status();

    /// <summary>Executa um ciclo de simulação agora.</summary>
    /// <remarks>
    /// Em cada ciclo:
    ///
    /// 1. robôs concluem as missões que já duraram `Simulador:DuracaoMissaoSegundos` e os sem bateria recarregam na base;
    /// 2. cada sensor ativo gera uma leitura plausível para o cenário (`Normal` ou `ChuvaForte`), processada
    ///    pelo mesmo fluxo de `POST /sensores/{id}/leituras`;
    /// 3. quadras esvaziadas voltam ao uso;
    /// 4. a cada `Simulador:AvaliarRiscoACadaCiclos` ciclos, o risco de todos os bairros é reavaliado.
    ///
    /// Funciona mesmo com o ciclo automático desligado.
    /// </remarks>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpPost("ciclo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<ResultadoCicloSimulacaoResponse> ExecutarCiclo(CancellationToken ct) => simulador.ExecutarCicloAsync(ct);
}

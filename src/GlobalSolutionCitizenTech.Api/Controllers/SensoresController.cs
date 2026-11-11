using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Api.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Sensores IoT e suas leituras.</summary>
[ApiController]
[Route("api/v1/sensores")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Sensores e leituras")]
public class SensoresController(ISensorService sensores) : ControllerBase
{
    /// <summary>Lista os sensores, com filtro opcional por bairro e tipo.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet]
    public Task<IReadOnlyList<SensorResponse>> Listar([FromQuery] Guid? bairroId, [FromQuery] TipoSensor? tipo,
        CancellationToken ct) => sensores.ListarAsync(bairroId, tipo, ct);

    /// <summary>Obtém um sensor com a última leitura.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<SensorResponse> Obter(Guid id, CancellationToken ct) => sensores.ObterAsync(id, ct);

    /// <summary>Cadastra um sensor.</summary>
    /// <remarks>
    /// - `BocaDeLobo` exige `limiar` (% de obstrução que dispara a desobstrução).
    /// - `NivelQuadra` exige `quadraId`.
    /// </remarks>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SensorResponse>> Criar(CriarSensorRequest request, CancellationToken ct)
    {
        var criado = await sensores.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    /// <summary>Ativa ou desativa um sensor. Sensor inativo não aceita leituras.</summary>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPatch("{id:guid}/ativacao")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<SensorResponse> AlterarAtivacao(Guid id, AlterarAtivacaoSensorRequest request, CancellationToken ct) =>
        sensores.AlterarAtivoAsync(id, request.Ativo!.Value, ct);

    /// <summary>Recebe uma leitura do sensor e executa as reações automáticas.</summary>
    /// <remarks>
    /// - **Boca de lobo** acima do limiar: abre missão de desobstrução e despacha o robô desentupidor
    ///   disponível do bairro com mais bateria (mínimo 30%). Sem robô, a missão fica pendente.
    /// - **Nível da quadra**: atualiza o nível; quadra em preparação que recebe água passa a `Retendo`;
    ///   a partir de 90% é emitido um alerta.
    ///
    /// A resposta lista as ações executadas.
    /// </remarks>
    [Authorize(Roles = Acesso.EnvioLeitura)]
    [HttpPost("{id:guid}/leituras")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeituraProcessadaResponse>> RegistrarLeitura(Guid id, RegistrarLeituraRequest request,
        CancellationToken ct)
    {
        var resultado = await sensores.RegistrarLeituraAsync(id, request.Valor, ct);
        return CreatedAtAction(nameof(ListarLeituras), new { id }, resultado);
    }

    /// <summary>Histórico de leituras do sensor, da mais recente para a mais antiga.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}/leituras")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<PaginaResponse<LeituraResponse>> ListarLeituras(Guid id, [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default) =>
        sensores.ListarLeiturasAsync(id, pagina, tamanhoPagina, ct);
}

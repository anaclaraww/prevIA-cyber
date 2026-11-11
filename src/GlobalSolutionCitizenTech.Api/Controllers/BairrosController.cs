using System.ComponentModel.DataAnnotations;
using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Api.Autenticacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlobalSolutionCitizenTech.Api.Controllers;

/// <summary>Bairros monitorados, avaliação de risco de alagamento e painel consolidado.</summary>
[ApiController]
[Route("api/v1/bairros")]
[Produces("application/json")]
[ProducesResponseType<ErroResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErroResponse>(StatusCodes.Status403Forbidden)]
[Tags("Bairros e risco")]
public class BairrosController(IBairroService bairros, IRiscoService risco, IPainelService painel) : ControllerBase
{
    /// <summary>Lista os bairros.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet]
    public Task<IReadOnlyList<BairroResponse>> Listar(CancellationToken ct) => bairros.ListarAsync(ct);

    /// <summary>Obtém um bairro.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<BairroResponse> Obter(Guid id, CancellationToken ct) => bairros.ObterAsync(id, ct);

    /// <summary>Cadastra um bairro.</summary>
    /// <remarks>O limiar de chuva é a quantidade de chuva prevista (mm) a partir da qual o bairro alaga.</remarks>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BairroResponse>> Criar(BairroRequest request, CancellationToken ct)
    {
        var criado = await bairros.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza um bairro.</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<BairroResponse> Atualizar(Guid id, BairroRequest request, CancellationToken ct) =>
        bairros.AtualizarAsync(id, request, ct);

    /// <summary>Remove um bairro sem quadras, sensores ou robôs vinculados.</summary>
    [Authorize(Roles = Acesso.Cadastro)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken ct)
    {
        await bairros.RemoverAsync(id, ct);
        return NoContent();
    }

    /// <summary>Avalia o risco de alagamento do bairro e executa as ações automáticas.</summary>
    /// <remarks>
    /// Consulta a previsão de chuva no Open-Meteo para as próximas <c>horas</c> e conta os entupimentos abertos.
    ///
    /// - **ALTO** (chuva ≥ limiar): quadras disponíveis vão para `EmPreparacao`, missões abertas viram prioridade alta e um alerta ALTO é emitido.
    /// - **MÉDIO** (chuva ≥ 50% do limiar): alerta MÉDIO.
    /// - 2 ou mais entupimentos abertos agravam o risco em um nível.
    /// </remarks>
    /// <param name="id">Id do bairro.</param>
    /// <param name="horas">Janela da previsão, de 1 a 48 horas (padrão 6).</param>
    /// <param name="ct">Token de cancelamento.</param>
    [Authorize(Roles = Acesso.Operacao)]
    [HttpPost("{id:guid}/avaliar-risco")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status502BadGateway)]
    public Task<ResultadoAvaliacaoRiscoResponse> AvaliarRisco(Guid id, [FromQuery, Range(1, 48)] int horas = RiscoService.HorasPadrao,
        CancellationToken ct = default) => risco.AvaliarAsync(id, horas, ct);

    /// <summary>Histórico das últimas 50 avaliações de risco do bairro.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}/avaliacoes-risco")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<AvaliacaoRiscoResponse>> HistoricoRisco(Guid id, CancellationToken ct) =>
        risco.HistoricoAsync(id, ct);

    /// <summary>Painel do bairro para a Defesa Civil: risco, quadras, robôs, missões abertas e alertas ativos.</summary>
    [Authorize(Roles = Acesso.Leitura)]
    [HttpGet("{id:guid}/painel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErroResponse>(StatusCodes.Status404NotFound)]
    public Task<PainelBairroResponse> Painel(Guid id, CancellationToken ct) => painel.ObterAsync(id, ct);
}

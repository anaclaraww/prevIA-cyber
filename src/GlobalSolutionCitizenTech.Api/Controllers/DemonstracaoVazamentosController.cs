using GlobalSolutionCitizenTech.Api.Autenticacao;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Privacidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Api.Controllers;

public record DemonstracaoSaudeRequest(string Nome, string CondicaoSaude);
public record DemonstracaoCpfRequest(string Cpf);
public record DemonstracaoLoginRequest(string Usuario, string Senha);

/// <summary>O que o código fez, para o painel e o vídeo. Nunca carrega o dado.</summary>
public record ResultadoDemonstracao(string Cenario, string CodigoVulneravel, string Esperado);

/// <summary>
/// Código <b>vulnerável de propósito</b>, para demonstrar a barreira de taint em ação. Cada action comete um
/// erro real de desenvolvedor; quem impede o vazamento é a camada de privacidade, não o controller.
/// </summary>
/// <remarks>Só responde com <c>Privacidade:CenariosDemonstracao = true</c> (ligado só em Development).</remarks>
[ApiController]
[Route("api/v1/demonstracao/vazamentos")]
[Produces("application/json")]
[Tags("Demonstração de vazamentos (cyber)")]
public class DemonstracaoVazamentosController(
    IOptions<ConfiguracaoPrivacidade> opcoes,
    ILogger<DemonstracaoVazamentosController> logger) : ControllerBase
{
    /// <summary>1. <c>logger.info(dados_de_saude)</c>: o log é bloqueado.</summary>
    [Authorize(Roles = Perfis.Admin)]
    [HttpPost("log-saude")]
    public ResultadoDemonstracao LogSaude(DemonstracaoSaudeRequest r)
    {
        Habilitado();
        // VULNERÁVEL: registra a condição de saúde no log.
        logger.LogInformation("Morador {Nome} cadastrado com condição de saúde {CondicaoSaude}", r.Nome, r.CondicaoSaude);
        return new("log-saude", "logger.LogInformation(..., r.CondicaoSaude)",
            "A linha de log é suprimida e vira um evento vermelho no painel.");
    }

    /// <summary>2. <c>console.log(senha)</c> com interpolação: só o taint pega (não há nome de parâmetro).</summary>
    [Authorize(Roles = Perfis.Admin)]
    [HttpPost("senha-no-log")]
    public ResultadoDemonstracao SenhaNoLog(DemonstracaoLoginRequest r)
    {
        Habilitado();
        // VULNERÁVEL: interpolação joga a senha dentro do texto do log.
#pragma warning disable CA2254
        logger.LogWarning($"Tentativa de login de {r.Usuario} com a senha {r.Senha}");
#pragma warning restore CA2254
        return new("senha-no-log", "logger.LogWarning($\"... {r.Senha}\")",
            "O taint reconhece o valor que entrou no campo senha e suprime a linha.");
    }

    /// <summary>3. Mensagem de erro que ecoa o CPF recebido: resposta e log saem mascarados.</summary>
    [Authorize(Roles = Perfis.Admin)]
    [HttpPost("erro-com-cpf")]
    public ResultadoDemonstracao ErroComCpf(DemonstracaoCpfRequest r)
    {
        Habilitado();
        // VULNERÁVEL: a exceção carrega o CPF; o tratamento de erros devolve e loga a mensagem.
        throw new DadosInvalidosException($"O CPF {r.Cpf} não consta na base da Defesa Civil.");
    }

    /// <summary>4. Endpoint anônimo que devolve a ficha do morador: a resposta é barrada com 403.</summary>
    [AllowAnonymous]
    [HttpGet("consulta-publica")]
    public object ConsultaPublica([FromQuery] string cpf)
    {
        Habilitado();
        // VULNERÁVEL: endpoint público devolvendo dado pessoal e de saúde.
        return new
        {
            nome = "Morador de demonstração",
            cpf,
            condicaoSaude = "Acamado, depende de oxigênio domiciliar",
            telefone = "(11) 98765-4321"
        };
    }

    private void Habilitado()
    {
        if (!opcoes.Value.CenariosDemonstracao)
            throw new RecursoNaoEncontradoException("Cenário de demonstração", HttpContext.Request.Path);
    }
}

using Anthropic;
using Anthropic.Models.Beta.Messages;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Privacidade.Pii;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Privacidade.Ia;

/// <summary>
/// Traduz o evento técnico de segurança para um cidadão leigo usando o Claude.
/// </summary>
/// <remarks>
/// O prompt é montado só com metadados (tipo, sink, categorias, descrição já mascarada) e, antes de sair,
/// passa de novo pelo <see cref="MotorPii"/>: se sobrar qualquer dado pessoal, a IA não é chamada.
/// Recusa, erro ou indisponibilidade caem no <see cref="TradutorRiscoOffline"/>; o painel nunca fica sem texto.
/// </remarks>
public sealed class TradutorRiscoClaude(
    AnthropicClient cliente,
    MotorPii motor,
    IOptions<ConfiguracaoPrivacidade> opcoes,
    ILogger<TradutorRiscoClaude> logger) : ITradutorRisco
{
    private const string BetaFallback = "server-side-fallback-2026-07-01";

    private const string InstrucoesSistema =
        "Você explica incidentes de segurança da Defesa Civil para moradores comuns. " +
        "Escreva em português do Brasil, para uma pessoa leiga de 60 anos: exatamente 2 frases curtas, " +
        "sem termos técnicos (nada de log, payload, HTTP, middleware, taint, PII ou criptografia), " +
        "dizendo o que aconteceu com o dado e se a pessoa precisa fazer algo. Tom calmo e direto. " +
        "Nunca invente números, nomes ou documentos. Responda só com as 2 frases.";

    public async Task<TraducaoRisco> TraduzirAsync(EventoSeguranca evento, CancellationToken ct)
    {
        var prompt = MontarPrompt(evento);

        // Última barreira: o texto que sai para a IA não pode carregar dado pessoal.
        if (motor.AnalisarTexto(prompt).Count > 0)
        {
            logger.LogWarning("Prompt de tradução continha dado pessoal; tradução feita offline.");
            return TradutorRiscoOffline.Traduzir(evento);
        }

        var ia = opcoes.Value.Ia;
        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(TimeSpan.FromSeconds(ia.TimeoutSegundos));

        var resposta = await cliente.Beta.Messages.Create(new MessageCreateParams
        {
            Model = ia.Modelo,
            MaxTokens = 2048,
            Betas = [BetaFallback],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            System = InstrucoesSistema,
            Messages = [new BetaUserMessageParam { Content = prompt }]
        }, tempo.Token);

        if (resposta.StopReason == BetaStopReason.Refusal)
        {
            logger.LogWarning("A IA recusou a tradução; usando o texto offline.");
            return TradutorRiscoOffline.Traduzir(evento);
        }

        var texto = string.Join(" ", resposta.Content
            .Select(b => b.TryPickText(out var t) ? t.Text : null)
            .Where(t => !string.IsNullOrWhiteSpace(t))).Trim();

        // A resposta também passa pelo motor: a IA não reintroduz dado pessoal no painel.
        if (texto.Length == 0 || motor.AnalisarTexto(texto).Count > 0)
            return TradutorRiscoOffline.Traduzir(evento);

        return new TraducaoRisco(texto, $"claude:{resposta.Model}");
    }

    public static string MontarPrompt(EventoSeguranca evento)
    {
        var categorias = string.Join(", ", evento.ListaCategorias().Select(Descrever));
        var acao = evento.Tipo switch
        {
            Domain.Enums.TipoEventoSeguranca.PiiProtegida => "o dado foi recebido e guardado protegido (cifrado ou mascarado)",
            Domain.Enums.TipoEventoSeguranca.VazamentoBloqueadoLog => "o sistema ia escrever o dado num registro interno e bloqueou",
            Domain.Enums.TipoEventoSeguranca.VazamentoMascaradoResposta => "uma resposta ia mostrar o dado inteiro e ele foi escondido antes de sair",
            Domain.Enums.TipoEventoSeguranca.VazamentoBloqueadoResposta => "alguém sem login tentou ver o dado e o acesso foi barrado",
            _ => "o sistema agiu para proteger o dado"
        };

        return $"""
            Traduza a falha técnica abaixo para um cidadão leigo de 60 anos, em 2 frases.
            Contexto: cadastro de moradores vulneráveis da Defesa Civil para prioridade de evacuação.
            Falha técnica: "{evento.DescricaoTecnica}"
            Tipo de dado envolvido: {categorias}
            O que o sistema fez: {acao}
            """;
    }

    private static string Descrever(Domain.Enums.CategoriaPii c) => c switch
    {
        Domain.Enums.CategoriaPii.Saude => "condição de saúde",
        Domain.Enums.CategoriaPii.Credencial => "senha de acesso",
        Domain.Enums.CategoriaPii.Documento => "documento (CPF/RG)",
        _ => "telefone ou e-mail"
    };
}

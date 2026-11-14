using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Privacidade.Ia;

/// <summary>
/// Textos prontos, usados quando não há chave de API, quando a IA falha ou recusa. A demonstração e o
/// painel continuam funcionando sem internet.
/// </summary>
public sealed class TradutorRiscoOffline : ITradutorRisco
{
    public const string Nome = "offline";

    public Task<TraducaoRisco> TraduzirAsync(EventoSeguranca evento, CancellationToken ct) =>
        Task.FromResult(Traduzir(evento));

    public static TraducaoRisco Traduzir(EventoSeguranca evento)
    {
        var categorias = evento.ListaCategorias();
        var principal = categorias.Contains(CategoriaPii.Saude) ? CategoriaPii.Saude
            : categorias.Contains(CategoriaPii.Credencial) ? CategoriaPii.Credencial
            : categorias.Contains(CategoriaPii.Documento) ? CategoriaPii.Documento
            : CategoriaPii.Contato;

        var dado = principal switch
        {
            CategoriaPii.Saude => "sua informação de saúde",
            CategoriaPii.Credencial => "uma senha de acesso",
            CategoriaPii.Documento => "seu documento (CPF ou RG)",
            _ => "seu telefone ou e-mail"
        };

        var mensagem = evento.Tipo switch
        {
            TipoEventoSeguranca.PiiProtegida when principal == CategoriaPii.Saude =>
                "Recebemos as informações de saúde e elas foram guardadas trancadas, como num cofre. " +
                "Só a equipe de resgate consegue abri-las numa emergência.",
            TipoEventoSeguranca.PiiProtegida =>
                $"Recebemos {dado} e guardamos tudo de forma protegida. " +
                "Ninguém da equipe vê o número completo.",
            TipoEventoSeguranca.VazamentoBloqueadoLog =>
                $"O sistema ia anotar {dado} num registro interno, mas bloqueou isso antes de acontecer. " +
                "Sua informação continua protegida e ninguém teve acesso a ela.",
            TipoEventoSeguranca.VazamentoBloqueadoResposta =>
                $"Alguém sem identificação tentou ver {dado}, e o sistema barrou o acesso. " +
                "Nenhum dado seu saiu do sistema.",
            TipoEventoSeguranca.VazamentoMascaradoResposta =>
                $"Uma tela do sistema ia mostrar {dado} por inteiro, mas escondemos a informação antes de aparecer. " +
                "Só ficou visível um pedaço que não identifica você.",
            _ => "Detectamos uma situação com seus dados e o sistema agiu para protegê-los. Não é preciso fazer nada."
        };

        return new TraducaoRisco(mensagem, Nome);
    }
}

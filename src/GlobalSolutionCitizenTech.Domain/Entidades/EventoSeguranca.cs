using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>
/// Registro de auditoria de um evento de privacidade: PII protegida na entrada ou vazamento bloqueado numa
/// saída. Guarda só metadados mascarados; a mensagem ao cidadão é preenchida depois, pela tradução via IA.
/// </summary>
public class EventoSeguranca : Entidade
{
    public TipoEventoSeguranca Tipo { get; private set; }
    public SinkSeguranca Sink { get; private set; }
    public Farol Farol { get; private set; }

    /// <summary>Categorias de PII envolvidas, separadas por vírgula (ex.: <c>Documento,Saude</c>).</summary>
    public string Categorias { get; private set; } = null!;

    /// <summary>Descrição técnica, sem nenhum dado pessoal (ex.: "Log tentou imprimir Saude em $.condicaoSaude").</summary>
    public string DescricaoTecnica { get; private set; } = null!;

    /// <summary>Método e rota onde aconteceu (ex.: <c>POST /api/v1/moradores</c>).</summary>
    public string Origem { get; private set; } = null!;

    public string? TraceId { get; private set; }

    /// <summary>A mesma falha em linguagem de cidadão, gerada pela IA (ou pelo texto offline de reserva).</summary>
    public string? MensagemCidadao { get; private set; }

    /// <summary>Quem traduziu: o modelo de IA usado ou <c>offline</c>.</summary>
    public string? TraduzidoPor { get; private set; }

    private EventoSeguranca() { }

    public EventoSeguranca(TipoEventoSeguranca tipo, SinkSeguranca sink, Farol farol, IEnumerable<CategoriaPii> categorias,
        string descricaoTecnica, string origem, string? traceId, DateTimeOffset ocorridoEm)
    {
        Tipo = tipo;
        Sink = sink;
        Farol = farol;
        Categorias = string.Join(',', categorias.Distinct().Order());
        DescricaoTecnica = descricaoTecnica;
        Origem = origem;
        TraceId = traceId;
        CriadoEm = ocorridoEm;
    }

    public IReadOnlyList<CategoriaPii> ListaCategorias() =>
        Categorias.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<CategoriaPii>).ToList();

    public void RegistrarTraducao(string mensagem, string traduzidoPor)
    {
        MensagemCidadao = mensagem;
        TraduzidoPor = traduzidoPor;
    }
}

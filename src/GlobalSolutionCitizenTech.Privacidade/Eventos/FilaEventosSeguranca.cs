using System.Threading.Channels;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Privacidade.Eventos;

/// <summary>Evento técnico ainda não gravado. Nunca carrega o valor do dado, só metadados.</summary>
public sealed record EventoPendente(TipoEventoSeguranca Tipo, SinkSeguranca Sink, IReadOnlyCollection<CategoriaPii> Categorias,
    string DescricaoTecnica, string Origem, string? TraceId, DateTimeOffset OcorridoEm);

/// <summary>
/// Fila em memória entre quem detecta (middleware, logger) e quem grava e traduz (processador em background).
/// O logger não pode gravar no banco sozinho: ele é chamado pelo próprio EF Core, e esperaria a IA no meio de um log.
/// </summary>
public sealed class FilaEventosSeguranca
{
    private readonly Channel<EventoPendente> _canal = Channel.CreateBounded<EventoPendente>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Publicar(EventoPendente evento) => _canal.Writer.TryWrite(evento);

    public ChannelReader<EventoPendente> Leitor => _canal.Reader;
}

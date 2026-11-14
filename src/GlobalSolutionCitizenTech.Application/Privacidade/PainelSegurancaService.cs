using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Privacidade;

public record EventoSegurancaResponse(Guid Id, DateTimeOffset OcorridoEm, TipoEventoSeguranca Tipo, SinkSeguranca Sink,
    Farol Farol, IReadOnlyList<CategoriaPii> Categorias, string DescricaoTecnica, string Origem, string? TraceId,
    string? MensagemCidadao, string? TraduzidoPor);

/// <summary>Dashboard executivo: o indicador Risco Cidadão e os eventos que o explicam.</summary>
/// <param name="IndicadorRiscoCidadao">0 a 100, calculado sobre a última hora.</param>
/// <param name="Farol">Farol consolidado do indicador.</param>
/// <param name="MensagemPrincipal">A mensagem ao cidadão do evento mais grave e recente da janela.</param>
/// <param name="Verdes">Dados protegidos na entrada.</param>
/// <param name="Amarelos">Vazamentos de documento ou contato barrados.</param>
/// <param name="Vermelhos">Vazamentos de saúde ou senha barrados, ou respostas anônimas bloqueadas.</param>
/// <param name="PorCategoria">Incidentes (amarelos e vermelhos) por tipo de dado.</param>
/// <param name="Eventos">Os 30 eventos mais recentes.</param>
/// <param name="AtualizadoEm">Momento do cálculo.</param>
public record PainelRiscoCidadaoResponse(int IndicadorRiscoCidadao, Farol Farol, string MensagemPrincipal,
    int Verdes, int Amarelos, int Vermelhos, IReadOnlyList<QuantidadePorCategoria> PorCategoria,
    IReadOnlyList<EventoSegurancaResponse> Eventos, DateTimeOffset AtualizadoEm);

/// <summary>Lista (e não dicionário): uma chave "Saude" no JSON seria lida como campo de saúde pelo motor de PII.</summary>
public record QuantidadePorCategoria(CategoriaPii Categoria, int Quantidade);

public interface IPainelSegurancaService
{
    Task<PainelRiscoCidadaoResponse> ObterAsync(CancellationToken ct);
}

public class PainelSegurancaService(IAppDbContext db, TimeProvider relogio) : IPainelSegurancaService
{
    public const string MensagemSemRisco = "Nenhum risco aos seus dados na última hora. Tudo o que você nos enviou está protegido.";

    public async Task<PainelRiscoCidadaoResponse> ObterAsync(CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var desde = agora - RiscoCidadao.Janela;
        var janela = await db.EventosSeguranca.AsNoTracking()
            .Where(e => e.CriadoEm >= desde)
            .OrderByDescending(e => e.CriadoEm)
            .ToListAsync(ct);

        var (indicador, farol) = RiscoCidadao.Consolidar(janela, agora);

        var principal = janela
            .Where(e => e.MensagemCidadao is not null)
            .OrderByDescending(e => e.Farol)
            .ThenByDescending(e => e.CriadoEm)
            .FirstOrDefault(e => e.Farol != Farol.Verde);

        var porCategoria = janela
            .Where(e => e.Farol != Farol.Verde)
            .SelectMany(e => e.ListaCategorias())
            .GroupBy(c => c)
            .Select(g => new QuantidadePorCategoria(g.Key, g.Count()))
            .OrderByDescending(q => q.Quantidade)
            .ToList();

        return new PainelRiscoCidadaoResponse(
            indicador, farol, principal?.MensagemCidadao ?? MensagemSemRisco,
            janela.Count(e => e.Farol == Farol.Verde),
            janela.Count(e => e.Farol == Farol.Amarelo),
            janela.Count(e => e.Farol == Farol.Vermelho),
            porCategoria,
            janela.Take(30).Select(e => e.ToResponse()).ToList(),
            agora);
    }
}

using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Application.Privacidade;

/// <summary>Regras determinísticas de risco: o farol não depende da IA, só o texto ao cidadão.</summary>
public static class RiscoCidadao
{
    /// <summary>Janela usada pelo indicador consolidado.</summary>
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(60);

    public const int PesoVermelho = 25;
    public const int PesoAmarelo = 8;

    /// <summary>Farol de um evento: o que vazaria e para onde.</summary>
    public static Farol Classificar(TipoEventoSeguranca tipo, IReadOnlyCollection<CategoriaPii> categorias)
    {
        var critico = categorias.Contains(CategoriaPii.Saude) || categorias.Contains(CategoriaPii.Credencial);
        return tipo switch
        {
            TipoEventoSeguranca.PiiProtegida => Farol.Verde,
            TipoEventoSeguranca.VazamentoBloqueadoResposta => Farol.Vermelho,
            TipoEventoSeguranca.VazamentoBloqueadoLog => critico ? Farol.Vermelho : Farol.Amarelo,
            TipoEventoSeguranca.VazamentoMascaradoResposta => critico ? Farol.Vermelho : Farol.Amarelo,
            _ => Farol.Amarelo
        };
    }

    /// <summary>
    /// Indicador Risco Cidadão (0 a 100) dos eventos da última hora: cada vermelho soma 25, cada amarelo 8.
    /// Verde não soma: é o sistema funcionando. 50 ou mais é vermelho, 15 ou mais é amarelo.
    /// </summary>
    public static (int Indicador, Farol Farol) Consolidar(IEnumerable<EventoSeguranca> eventos, DateTimeOffset agora)
    {
        var recentes = eventos.Where(e => e.CriadoEm >= agora - Janela).ToList();
        var pontos = recentes.Count(e => e.Farol == Farol.Vermelho) * PesoVermelho
                     + recentes.Count(e => e.Farol == Farol.Amarelo) * PesoAmarelo;
        var indicador = Math.Min(100, pontos);
        var farol = indicador >= 50 ? Farol.Vermelho : indicador >= 15 ? Farol.Amarelo : Farol.Verde;
        return (indicador, farol);
    }
}

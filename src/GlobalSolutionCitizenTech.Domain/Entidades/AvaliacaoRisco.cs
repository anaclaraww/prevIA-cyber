using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>Resultado de uma avaliação de risco de alagamento (histórico append-only).</summary>
public class AvaliacaoRisco : Entidade
{
    public Guid BairroId { get; private set; }
    public decimal ChuvaPrevistaMm { get; private set; }
    public int HorasConsideradas { get; private set; }
    public decimal LimiarChuvaMm { get; private set; }
    public int EntupimentosAbertos { get; private set; }
    public NivelRisco Nivel { get; private set; }

    private AvaliacaoRisco() { }

    public AvaliacaoRisco(Guid bairroId, decimal chuvaPrevistaMm, int horasConsideradas,
        decimal limiarChuvaMm, int entupimentosAbertos, NivelRisco nivel)
    {
        BairroId = bairroId;
        ChuvaPrevistaMm = chuvaPrevistaMm;
        HorasConsideradas = horasConsideradas;
        LimiarChuvaMm = limiarChuvaMm;
        EntupimentosAbertos = entupimentosAbertos;
        Nivel = nivel;
    }
}

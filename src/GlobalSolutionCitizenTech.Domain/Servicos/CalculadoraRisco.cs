using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Servicos;

/// <summary>
/// Regra de risco de alagamento.
/// <list type="number">
/// <item>Nível base pela razão chuva prevista / limiar do bairro: ≥ 100% ALTO, ≥ 50% MÉDIO, abaixo BAIXO.</item>
/// <item>Com <see cref="EntupimentosParaAgravar"/> ou mais entupimentos abertos, o risco sobe um nível
/// (a drenagem entupida reduz a capacidade de escoamento).</item>
/// </list>
/// </summary>
public static class CalculadoraRisco
{
    public const int EntupimentosParaAgravar = 2;

    public static NivelRisco Calcular(decimal chuvaPrevistaMm, decimal limiarChuvaMm, int entupimentosAbertos)
    {
        if (limiarChuvaMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(limiarChuvaMm), "O limiar deve ser maior que zero.");
        if (chuvaPrevistaMm < 0)
            throw new ArgumentOutOfRangeException(nameof(chuvaPrevistaMm), "A chuva prevista não pode ser negativa.");

        var razao = chuvaPrevistaMm / limiarChuvaMm;
        var nivel = razao switch
        {
            >= 1m => NivelRisco.Alto,
            >= 0.5m => NivelRisco.Medio,
            _ => NivelRisco.Baixo
        };

        if (entupimentosAbertos >= EntupimentosParaAgravar && nivel < NivelRisco.Alto)
            nivel++;

        return nivel;
    }
}

using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Domain.Servicos;

namespace GlobalSolutionCitizenTech.UnitTests.Dominio;

public class CalculadoraRiscoTests
{
    [Theory]
    [InlineData(0, 20, 0, NivelRisco.Baixo)]
    [InlineData(9.99, 20, 0, NivelRisco.Baixo)]
    [InlineData(10, 20, 0, NivelRisco.Medio)]   // 50% do limiar
    [InlineData(19.99, 20, 0, NivelRisco.Medio)]
    [InlineData(20, 20, 0, NivelRisco.Alto)]    // atingiu o limiar
    [InlineData(60, 20, 0, NivelRisco.Alto)]
    public void Calcular_PelaRazaoChuvaLimiar(decimal chuva, decimal limiar, int entupimentos, NivelRisco esperado) =>
        Assert.Equal(esperado, CalculadoraRisco.Calcular(chuva, limiar, entupimentos));

    [Theory]
    [InlineData(0, 20, 1, NivelRisco.Baixo)]    // 1 entupimento não agrava
    [InlineData(0, 20, 2, NivelRisco.Medio)]    // 2 entupimentos agravam um nível
    [InlineData(10, 20, 3, NivelRisco.Alto)]
    [InlineData(25, 20, 5, NivelRisco.Alto)]    // já ALTO continua ALTO
    public void Calcular_EntupimentosAgravamUmNivel(decimal chuva, decimal limiar, int entupimentos, NivelRisco esperado) =>
        Assert.Equal(esperado, CalculadoraRisco.Calcular(chuva, limiar, entupimentos));

    [Fact]
    public void Calcular_LimiarZero_Lanca() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculadoraRisco.Calcular(10, 0, 0));

    [Fact]
    public void Calcular_ChuvaNegativa_Lanca() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculadoraRisco.Calcular(-1, 20, 0));
}

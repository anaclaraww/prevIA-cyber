using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.UnitTests.Dominio;

public class QuadraTests
{
    private static Quadra NovaQuadra(decimal capacidade = 420m) =>
        new("Quadra Teste", Guid.NewGuid(), "01310100", "Av. Paulista", "1", -23.5, -46.6, capacidade);

    private static Quadra QuadraNoStatus(StatusQuadra status)
    {
        var quadra = NovaQuadra();
        var caminho = status switch
        {
            StatusQuadra.Disponivel => Array.Empty<StatusQuadra>(),
            StatusQuadra.EmPreparacao => [StatusQuadra.EmPreparacao],
            StatusQuadra.Retendo => [StatusQuadra.EmPreparacao, StatusQuadra.Retendo],
            StatusQuadra.Esvaziando => [StatusQuadra.EmPreparacao, StatusQuadra.Retendo, StatusQuadra.Esvaziando],
            StatusQuadra.Manutencao => [StatusQuadra.Manutencao],
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        foreach (var passo in caminho) quadra.AlterarStatus(passo);
        return quadra;
    }

    [Theory]
    [InlineData(StatusQuadra.Disponivel, StatusQuadra.EmPreparacao)]
    [InlineData(StatusQuadra.Disponivel, StatusQuadra.Manutencao)]
    [InlineData(StatusQuadra.EmPreparacao, StatusQuadra.Retendo)]
    [InlineData(StatusQuadra.EmPreparacao, StatusQuadra.Disponivel)]
    [InlineData(StatusQuadra.Retendo, StatusQuadra.Esvaziando)]
    [InlineData(StatusQuadra.Esvaziando, StatusQuadra.Disponivel)]
    [InlineData(StatusQuadra.Manutencao, StatusQuadra.Disponivel)]
    public void AlterarStatus_TransicaoPermitida_MudaStatus(StatusQuadra origem, StatusQuadra destino)
    {
        var quadra = QuadraNoStatus(origem);

        quadra.AlterarStatus(destino);

        Assert.Equal(destino, quadra.Status);
    }

    [Theory]
    [InlineData(StatusQuadra.Disponivel, StatusQuadra.Retendo)]
    [InlineData(StatusQuadra.Disponivel, StatusQuadra.Esvaziando)]
    [InlineData(StatusQuadra.Retendo, StatusQuadra.Disponivel)]
    [InlineData(StatusQuadra.Esvaziando, StatusQuadra.Retendo)]
    [InlineData(StatusQuadra.Manutencao, StatusQuadra.EmPreparacao)]
    [InlineData(StatusQuadra.Disponivel, StatusQuadra.Disponivel)]
    public void AlterarStatus_TransicaoInvalida_LancaRegraNegocio(StatusQuadra origem, StatusQuadra destino)
    {
        var quadra = QuadraNoStatus(origem);

        var ex = Assert.Throws<RegraNegocioException>(() => quadra.AlterarStatus(destino));
        Assert.Contains("Transição de status inválida", ex.Message);
        Assert.Equal(origem, quadra.Status);
    }

    [Fact]
    public void AtualizarNivel_EmPreparacaoRecebendoAgua_PassaParaRetendo()
    {
        var quadra = QuadraNoStatus(StatusQuadra.EmPreparacao);

        quadra.AtualizarNivel(10);

        Assert.Equal(StatusQuadra.Retendo, quadra.Status);
    }

    [Fact]
    public void AtualizarNivel_Disponivel_NaoMudaStatus()
    {
        var quadra = NovaQuadra();

        quadra.AtualizarNivel(10);

        Assert.Equal(StatusQuadra.Disponivel, quadra.Status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    public void AtualizarNivel_ForaDaFaixa_LancaRegraNegocio(decimal nivel) =>
        Assert.Throws<RegraNegocioException>(() => NovaQuadra().AtualizarNivel(nivel));

    [Fact]
    public void VolumeRetido_CalculaPelaCapacidade()
    {
        var quadra = NovaQuadra(420m);

        quadra.AtualizarNivel(50);

        Assert.Equal(210m, quadra.VolumeRetidoM3);
        Assert.False(quadra.ProximaDaCapacidade);
    }

    [Fact]
    public void ProximaDaCapacidade_APartirDe90Porcento()
    {
        var quadra = NovaQuadra();

        quadra.AtualizarNivel(90);

        Assert.True(quadra.ProximaDaCapacidade);
    }

    [Fact]
    public void VoltarParaDisponivel_ZeraNivel()
    {
        var quadra = QuadraNoStatus(StatusQuadra.Retendo);
        quadra.AtualizarNivel(80);
        quadra.AlterarStatus(StatusQuadra.Esvaziando);

        quadra.AlterarStatus(StatusQuadra.Disponivel);

        Assert.Equal(0, quadra.NivelAtualPercentual);
    }

    [Fact]
    public void Criar_CapacidadeZero_LancaRegraNegocio() =>
        Assert.Throws<RegraNegocioException>(() => NovaQuadra(0));
}

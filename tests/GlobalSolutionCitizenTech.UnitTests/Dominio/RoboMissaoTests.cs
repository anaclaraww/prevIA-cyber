using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Domain.Servicos;

namespace GlobalSolutionCitizenTech.UnitTests.Dominio;

public class RoboMissaoTests
{
    private static readonly Guid Bairro = Guid.NewGuid();
    private static readonly DateTimeOffset Agora = DateTimeOffset.UtcNow;

    private static Missao NovaDesobstrucao() =>
        new(TipoMissao.Desobstrucao, Bairro, null, PrioridadeMissao.Normal, "teste");

    [Fact]
    public void Seletor_EscolheDesentupidorDoBairroComMaisBateria()
    {
        var robos = new[]
        {
            new Robo("DES-A", TipoRobo.Desentupidor, Bairro, 60),
            new Robo("DES-B", TipoRobo.Desentupidor, Bairro, 95),
            new Robo("DES-OUTRO", TipoRobo.Desentupidor, Guid.NewGuid(), 100),
            new Robo("INS-A", TipoRobo.Inspetor, Bairro, 100),
        };

        var escolhido = SeletorRobo.Escolher(robos, Bairro, TipoMissao.Desobstrucao);

        Assert.Equal("DES-B", escolhido?.Codigo);
    }

    [Fact]
    public void Seletor_IgnoraBateriaAbaixoDoMinimo()
    {
        var robos = new[] { new Robo("DES-A", TipoRobo.Desentupidor, Bairro, Robo.BateriaMinimaParaMissao - 1) };

        Assert.Null(SeletorRobo.Escolher(robos, Bairro, TipoMissao.Desobstrucao));
    }

    [Fact]
    public void Seletor_IgnoraRoboEmMissaoOuManutencao()
    {
        var emMissao = new Robo("DES-A", TipoRobo.Desentupidor, Bairro, 100);
        NovaDesobstrucao().AtribuirRobo(emMissao, Agora);
        var manutencao = new Robo("DES-B", TipoRobo.Desentupidor, Bairro, 100);
        manutencao.EnviarParaManutencao();

        Assert.Null(SeletorRobo.Escolher([emMissao, manutencao], Bairro, TipoMissao.Desobstrucao));
    }

    [Fact]
    public void Seletor_InspecaoUsaRoboInspetor()
    {
        var robos = new[]
        {
            new Robo("DES-A", TipoRobo.Desentupidor, Bairro, 100),
            new Robo("INS-A", TipoRobo.Inspetor, Bairro, 50),
        };

        Assert.Equal("INS-A", SeletorRobo.Escolher(robos, Bairro, TipoMissao.Inspecao)?.Codigo);
    }

    [Fact]
    public void AtribuirRobo_ColocaMissaoEmAndamentoERoboEmMissao()
    {
        var robo = new Robo("DES-A", TipoRobo.Desentupidor, Bairro, 80);
        var missao = NovaDesobstrucao();

        missao.AtribuirRobo(robo, Agora);

        Assert.Equal(StatusMissao.EmAndamento, missao.Status);
        Assert.Equal(robo.Id, missao.RoboId);
        Assert.Equal(StatusRobo.EmMissao, robo.Status);
        Assert.Equal(Agora, missao.IniciadaEm);
    }

    [Fact]
    public void AtribuirRobo_TipoIncompativel_Lanca()
    {
        var inspetor = new Robo("INS-A", TipoRobo.Inspetor, Bairro, 80);

        Assert.Throws<RegraNegocioException>(() => NovaDesobstrucao().AtribuirRobo(inspetor, Agora));
    }

    [Fact]
    public void Concluir_ConsomeBateriaELiberaRobo()
    {
        var robo = new Robo("DES-A", TipoRobo.Desentupidor, Bairro, 80);
        var missao = NovaDesobstrucao();
        missao.AtribuirRobo(robo, Agora);

        missao.Concluir(Agora);

        Assert.Equal(StatusMissao.Concluida, missao.Status);
        Assert.Equal(StatusRobo.Disponivel, robo.Status);
        Assert.Equal(80 - Robo.ConsumoBateriaPorMissao, robo.Bateria);
    }

    [Fact]
    public void Concluir_MissaoPendente_Lanca() =>
        Assert.Throws<RegraNegocioException>(() => NovaDesobstrucao().Concluir(Agora));

    [Fact]
    public void Recarregar_EmMissao_Lanca()
    {
        var robo = new Robo("DES-A", TipoRobo.Desentupidor, Bairro, 80);
        NovaDesobstrucao().AtribuirRobo(robo, Agora);

        Assert.Throws<RegraNegocioException>(robo.Recarregar);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void CriarRobo_BateriaInvalida_Lanca(int bateria) =>
        Assert.Throws<RegraNegocioException>(() => new Robo("DES-A", TipoRobo.Desentupidor, Bairro, bateria));
}

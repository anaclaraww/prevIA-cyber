using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.UnitTests.Aplicacao;

public class SensorServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task LeituraBocaDeLoboAcimaDoLimiar_CriaMissaoEAtribuiRobo()
    {
        using var c = new Cenario();

        var resultado = await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);

        var missao = await c.Db.Missoes.Include(m => m.Robo).SingleAsync();
        Assert.Equal(TipoMissao.Desobstrucao, missao.Tipo);
        Assert.Equal(StatusMissao.EmAndamento, missao.Status);
        Assert.Equal("DES-1", missao.Robo!.Codigo);
        Assert.Equal(StatusRobo.EmMissao, missao.Robo.Status);
        Assert.Contains(resultado.AcoesExecutadas, a => a.Contains("atribuída ao robô DES-1"));
        Assert.Equal(1, await c.Db.Leituras.CountAsync());
    }

    [Fact]
    public async Task LeituraBocaDeLoboAbaixoDoLimiar_NaoCriaMissao()
    {
        using var c = new Cenario();

        var resultado = await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 40, Ct);

        Assert.Empty(resultado.AcoesExecutadas);
        Assert.False(await c.Db.Missoes.AnyAsync());
    }

    [Fact]
    public async Task SegundaLeituraDoMesmoEntupimento_NaoDuplicaMissao()
    {
        using var c = new Cenario();
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);

        var resultado = await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 95, Ct);

        Assert.Equal(1, await c.Db.Missoes.CountAsync());
        Assert.Contains(resultado.AcoesExecutadas, a => a.Contains("já possui missão"));
    }

    [Fact]
    public async Task SemRoboComBateria_MissaoFicaPendente()
    {
        using var c = new Cenario(bateriaDesentupidor: 20);

        var resultado = await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);

        var missao = await c.Db.Missoes.SingleAsync();
        Assert.Equal(StatusMissao.Pendente, missao.Status);
        Assert.Null(missao.RoboId);
        Assert.Contains(resultado.AcoesExecutadas, a => a.Contains("aguardando robô"));
    }

    [Fact]
    public async Task EntupimentoComRiscoAltoVigente_MissaoNasceComPrioridadeAlta()
    {
        using var c = new Cenario();
        c.Previsao.ChuvaMm = 30;
        await c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct);

        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);

        Assert.Equal(PrioridadeMissao.Alta, (await c.Db.Missoes.SingleAsync()).Prioridade);
    }

    [Fact]
    public async Task LeituraNivelQuadraEmPreparacao_PassaParaRetendo()
    {
        using var c = new Cenario();
        c.Quadra.AlterarStatus(StatusQuadra.EmPreparacao);
        await c.Db.SaveChangesAsync();

        var resultado = await c.Sensores.RegistrarLeituraAsync(c.NivelQuadra.Id, 40, Ct);

        Assert.Equal(StatusQuadra.Retendo, c.Quadra.Status);
        Assert.Equal(40, c.Quadra.NivelAtualPercentual);
        Assert.Contains(resultado.AcoesExecutadas, a => a.Contains("EmPreparacao para Retendo"));
        Assert.False(await c.Db.Alertas.AnyAsync());
    }

    [Fact]
    public async Task LeituraNivelQuadraAcimaDe90_CriaAlertaUmaVez()
    {
        using var c = new Cenario();

        await c.Sensores.RegistrarLeituraAsync(c.NivelQuadra.Id, 92, Ct);
        await c.Sensores.RegistrarLeituraAsync(c.NivelQuadra.Id, 96, Ct);

        var alerta = await c.Db.Alertas.SingleAsync();
        Assert.Equal(c.Quadra.Id, alerta.QuadraId);
        Assert.Equal(NivelRisco.Alto, alerta.Nivel);
    }

    [Fact]
    public async Task SensorInexistente_LancaNaoEncontrado()
    {
        using var c = new Cenario();

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            c.Sensores.RegistrarLeituraAsync(Guid.NewGuid(), 10, Ct));
    }

    [Fact]
    public async Task ListarLeituras_PaginaDaMaisRecente()
    {
        using var c = new Cenario();
        foreach (var valor in new decimal[] { 10, 20, 30 })
            await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, valor, Ct);

        var pagina = await c.Sensores.ListarLeiturasAsync(c.BocaDeLobo1.Id, 1, 2, Ct);

        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.Itens.Count);
        Assert.Equal(30, pagina.Itens[0].Valor);
    }
}

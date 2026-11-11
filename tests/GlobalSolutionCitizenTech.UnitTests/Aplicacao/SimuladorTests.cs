using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Application.Simulacao;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.UnitTests.Aplicacao;

public class SimuladorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class CepNulo : ICepClient
    {
        public Task<EnderecoCep?> ConsultarAsync(string cep, CancellationToken ct) => Task.FromResult<EnderecoCep?>(null);
    }

    private static SimuladorSensores Simulador(Cenario c, CenarioSimulacao cenario = CenarioSimulacao.ChuvaForte,
        int avaliarRiscoACada = 0, int duracaoMissao = 0)
    {
        var opcoes = Options.Create(new ConfiguracaoSimulador
        {
            Cenario = cenario, AvaliarRiscoACadaCiclos = avaliarRiscoACada, DuracaoMissaoSegundos = duracaoMissao, Semente = 7
        });
        return new SimuladorSensores(c.Db, c.Sensores, c.Missoes, c.Robos, new QuadraService(c.Db, new CepNulo()), c.Risco,
            new EstadoSimulacao(opcoes), opcoes, TimeProvider.System);
    }

    [Fact]
    public async Task Ciclo_GeraLeituraParaCadaSensorAtivo_ExcetoQuadraParada()
    {
        using var c = new Cenario();
        var simulador = Simulador(c, CenarioSimulacao.Normal);

        var resultado = await simulador.ExecutarCicloAsync(Ct);

        Assert.Equal(1, resultado.Ciclo);
        Assert.Equal(["BL-1", "BL-2"], resultado.Leituras.Select(l => l.Sensor).Order());
        Assert.All(resultado.Leituras, l => Assert.InRange(l.Valor, 0m, 100m));
        Assert.Equal(2, await c.Db.Leituras.CountAsync());
    }

    [Fact]
    public async Task Ciclo_SensorInativo_Ignorado()
    {
        using var c = new Cenario();
        c.BocaDeLobo2.Desativar();
        await c.Db.SaveChangesAsync();

        var resultado = await Simulador(c).ExecutarCicloAsync(Ct);

        Assert.DoesNotContain(resultado.Leituras, l => l.Sensor == "BL-2");
    }

    [Fact]
    public async Task ChuvaForte_EntupimentoPassaDoLimiarEAbreMissao()
    {
        using var c = new Cenario();
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 69, Ct);
        var simulador = Simulador(c);

        var resultado = await simulador.ExecutarCicloAsync(Ct);

        Assert.True(resultado.Leituras.Single(l => l.Sensor == "BL-1").Valor >= 73);
        var missao = await c.Db.Missoes.SingleAsync(m => m.SensorId == c.BocaDeLobo1.Id);
        Assert.Equal(StatusMissao.EmAndamento, missao.Status);
    }

    [Fact]
    public async Task MissaoConcluida_BocaDeLoboVoltaLimpa()
    {
        using var c = new Cenario();
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 69, Ct);
        var simulador = Simulador(c, duracaoMissao: 0);
        await simulador.ExecutarCicloAsync(Ct);                    // abre a missão

        var segundo = await simulador.ExecutarCicloAsync(Ct);      // robô conclui; sensor volta limpo

        Assert.Contains(segundo.AcoesExecutadas, a => a.Contains("DES-1 concluiu"));
        Assert.InRange(segundo.Leituras.Single(l => l.Sensor == "BL-1").Valor, 5m, 20m);
    }

    [Fact]
    public async Task MissaoAindaNoPrazo_NaoEConcluida()
    {
        using var c = new Cenario();
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);

        var resultado = await Simulador(c, duracaoMissao: 3600).ExecutarCicloAsync(Ct);

        Assert.DoesNotContain(resultado.AcoesExecutadas, a => a.Contains("concluiu"));
        Assert.Equal(StatusMissao.EmAndamento, (await c.Db.Missoes.SingleAsync()).Status);
    }

    [Fact]
    public async Task RoboSemBateria_RecarregaEAssumeMissaoPendente()
    {
        using var c = new Cenario(bateriaDesentupidor: 20);
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);     // missão fica pendente

        var resultado = await Simulador(c, duracaoMissao: 3600).ExecutarCicloAsync(Ct);

        Assert.Contains(resultado.AcoesExecutadas, a => a.Contains("DES-1 recarregado") && a.Contains("missão pendente"));
        var robo = await c.Db.Robos.SingleAsync();
        Assert.Equal(StatusRobo.EmMissao, robo.Status);
        Assert.Equal(100, robo.Bateria);
    }

    [Fact]
    public async Task QuadraEmPreparacao_EncheNoTemporalEPassaARetendo()
    {
        using var c = new Cenario();
        c.Quadra.AlterarStatus(StatusQuadra.EmPreparacao);
        await c.Db.SaveChangesAsync();

        var resultado = await Simulador(c).ExecutarCicloAsync(Ct);

        Assert.Contains(resultado.Leituras, l => l.Sensor == "NIV-1" && l.Valor >= 6);
        Assert.Equal(StatusQuadra.Retendo, c.Quadra.Status);
    }

    [Fact]
    public async Task QuadraEsvaziandoSemAgua_VoltaADisponivel()
    {
        using var c = new Cenario();
        c.Quadra.AlterarStatus(StatusQuadra.EmPreparacao);
        c.Quadra.AlterarStatus(StatusQuadra.Retendo);
        c.Quadra.AlterarStatus(StatusQuadra.Esvaziando);
        await c.Db.SaveChangesAsync();

        var resultado = await Simulador(c).ExecutarCicloAsync(Ct);

        Assert.Equal(StatusQuadra.Disponivel, c.Quadra.Status);
        Assert.Contains(resultado.AcoesExecutadas, a => a.Contains("liberada para uso esportivo"));
    }

    [Fact]
    public async Task RiscoAlto_PreparaQuadras_ERiscoBaixo_LiberaAAgua()
    {
        using var c = new Cenario();
        c.BocaDeLobo1.Desativar();   // sem entupimentos: o risco depende só da previsão
        c.BocaDeLobo2.Desativar();
        await c.Db.SaveChangesAsync();
        var simulador = Simulador(c, avaliarRiscoACada: 1);

        c.Previsao.ChuvaMm = 30;
        var temporal = await simulador.ExecutarCicloAsync(Ct);
        Assert.Contains(temporal.AcoesExecutadas, a => a.StartsWith("Risco de Bairro Teste: Alto"));
        Assert.Equal(StatusQuadra.EmPreparacao, c.Quadra.Status);

        await simulador.ExecutarCicloAsync(Ct);                    // a quadra começa a encher
        Assert.Equal(StatusQuadra.Retendo, c.Quadra.Status);

        c.Previsao.ChuvaMm = 0;
        var passou = await simulador.ExecutarCicloAsync(Ct);

        Assert.Contains(passou.AcoesExecutadas, a => a.Contains("começou a liberar a água"));
        Assert.Equal(StatusQuadra.Esvaziando, c.Quadra.Status);
    }

    [Fact]
    public async Task PrevisaoIndisponivel_CicloContinua()
    {
        using var c = new Cenario();
        c.Previsao.Falhar = true;

        var resultado = await Simulador(c, avaliarRiscoACada: 1).ExecutarCicloAsync(Ct);

        Assert.Contains(resultado.AcoesExecutadas, a => a.Contains("não avaliado"));
        Assert.NotEmpty(resultado.Leituras);
    }

    [Fact]
    public async Task PrevisaoSimulada_UsaValorConfigurado()
    {
        var previsao = new PrevisaoSimuladaClient(Options.Create(new ConfiguracaoSimulador { ChuvaPrevistaMm = 42.5m }));

        Assert.Equal(42.5m, await previsao.ObterChuvaPrevistaMmAsync(0, 0, 6, Ct));
    }

    [Fact]
    public async Task Status_RefleteConfiguracaoECiclos()
    {
        using var c = new Cenario();
        var simulador = Simulador(c, CenarioSimulacao.Normal);
        await simulador.ExecutarCicloAsync(Ct);

        var status = simulador.Status();

        Assert.Equal(CenarioSimulacao.Normal, status.Cenario);
        Assert.Equal(1, status.CiclosExecutados);
        Assert.False(status.Habilitado);
    }
}

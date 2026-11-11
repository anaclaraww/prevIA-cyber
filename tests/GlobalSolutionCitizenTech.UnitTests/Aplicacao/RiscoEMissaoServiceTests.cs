using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.UnitTests.Aplicacao;

public class RiscoEMissaoServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task RiscoAlto_PreparaQuadrasElevaMissoesEEmiteAlerta()
    {
        using var c = new Cenario(bateriaDesentupidor: 20); // missão fica pendente com prioridade normal
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);
        c.Previsao.ChuvaMm = 25; // limiar 20

        var resultado = await c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct);

        Assert.Equal(NivelRisco.Alto, resultado.Avaliacao.Nivel);
        Assert.Equal(StatusQuadra.EmPreparacao, c.Quadra.Status);
        Assert.Equal(PrioridadeMissao.Alta, (await c.Db.Missoes.SingleAsync()).Prioridade);
        var alerta = await c.Db.Alertas.SingleAsync();
        Assert.Equal(NivelRisco.Alto, alerta.Nivel);
        Assert.Null(alerta.QuadraId);
        Assert.Equal(1, await c.Db.AvaliacoesRisco.CountAsync());
    }

    [Fact]
    public async Task RiscoAltoRepetido_NaoDuplicaAlerta()
    {
        using var c = new Cenario();
        c.Previsao.ChuvaMm = 25;

        await c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct);
        var segundo = await c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct);

        Assert.Equal(1, await c.Db.Alertas.CountAsync());
        Assert.Contains(segundo.AcoesExecutadas, a => a.Contains("Já existe alerta"));
    }

    [Fact]
    public async Task RiscoMedio_EmiteAlertaMedioSemMexerNasQuadras()
    {
        using var c = new Cenario();
        c.Previsao.ChuvaMm = 12;

        var resultado = await c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct);

        Assert.Equal(NivelRisco.Medio, resultado.Avaliacao.Nivel);
        Assert.Equal(StatusQuadra.Disponivel, c.Quadra.Status);
        Assert.Equal(NivelRisco.Medio, (await c.Db.Alertas.SingleAsync()).Nivel);
    }

    [Fact]
    public async Task RiscoBaixo_NenhumaAcao()
    {
        using var c = new Cenario();
        c.Previsao.ChuvaMm = 1;

        var resultado = await c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct);

        Assert.Equal(NivelRisco.Baixo, resultado.Avaliacao.Nivel);
        Assert.False(await c.Db.Alertas.AnyAsync());
    }

    [Fact]
    public async Task DoisEntupimentosAbertos_AgravamRisco()
    {
        using var c = new Cenario();
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo2.Id, 85, Ct);
        c.Previsao.ChuvaMm = 0;

        var resultado = await c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct);

        Assert.Equal(2, resultado.Avaliacao.EntupimentosAbertos);
        Assert.Equal(NivelRisco.Medio, resultado.Avaliacao.Nivel);
    }

    [Fact]
    public async Task PrevisaoIndisponivel_PropagaServicoExterno()
    {
        using var c = new Cenario();
        c.Previsao.Falhar = true;

        await Assert.ThrowsAsync<ServicoExternoException>(() => c.Risco.AvaliarAsync(c.Bairro.Id, 6, Ct));
        Assert.False(await c.Db.AvaliacoesRisco.AnyAsync());
    }

    [Fact]
    public async Task ConcluirMissao_RoboAssumeProximaPendenteMaisPrioritaria()
    {
        using var c = new Cenario();
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);  // DES-1 assume
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo2.Id, 85, Ct);  // fica pendente
        var primeira = await c.Db.Missoes.SingleAsync(m => m.SensorId == c.BocaDeLobo1.Id);

        var resultado = await c.Missoes.ConcluirAsync(primeira.Id, Ct);

        Assert.Equal(StatusMissao.Concluida, resultado.Missao.Status);
        Assert.NotNull(resultado.ProximaMissaoDoRobo);
        Assert.Equal(c.BocaDeLobo2.Id, resultado.ProximaMissaoDoRobo!.SensorId);
        Assert.Equal(StatusMissao.EmAndamento, resultado.ProximaMissaoDoRobo.Status);
        var robo = await c.Db.Robos.SingleAsync();
        Assert.Equal(90 - Robo.ConsumoBateriaPorMissao, robo.Bateria);
    }

    [Fact]
    public async Task NovoRobo_AssumeMissaoPendenteDoBairro()
    {
        using var c = new Cenario(comDesentupidor: false);
        await c.Sensores.RegistrarLeituraAsync(c.BocaDeLobo1.Id, 85, Ct);

        var robo = await c.Robos.CriarAsync(new CriarRoboRequest("DES-NOVO", TipoRobo.Desentupidor, c.Bairro.Id, 100), Ct);

        Assert.Equal(StatusRobo.EmMissao, robo.Status);
        Assert.Equal(StatusMissao.EmAndamento, (await c.Db.Missoes.SingleAsync()).Status);
    }

    [Fact]
    public async Task CriarMissaoInspecaoManual_DespachaInspetor()
    {
        using var c = new Cenario();
        c.Db.Robos.Add(new Robo("INS-1", TipoRobo.Inspetor, c.Bairro.Id, 70));
        await c.Db.SaveChangesAsync();

        var missao = await c.Missoes.CriarAsync(
            new CriarMissaoRequest(TipoMissao.Inspecao, c.Bairro.Id, null), Ct);

        Assert.Equal("INS-1", missao.RoboCodigo);
        Assert.Equal("Inspeção preventiva das galerias.", missao.Descricao);
    }
}

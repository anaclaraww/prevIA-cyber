using System.Net;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Simulacao;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.IntegrationTests.Infra;

namespace GlobalSolutionCitizenTech.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class SimuladorApiTests(ApiFactory factory)
{
    private readonly HttpClient _admin = factory.Cliente();

    [Fact]
    public async Task Status_MostraConfiguracao()
    {
        var status = await (await _admin.GetAsync("/api/v1/simulador")).LerAsync<StatusSimuladorResponse>();

        Assert.False(status.Habilitado);
        Assert.Equal(CenarioSimulacao.ChuvaForte, status.Cenario);
    }

    [Fact]
    public async Task Ciclos_EntupimentoViraMissaoEDepoisEConcluida()
    {
        var bairro = await (await _admin.PostJsonAsync("/api/v1/bairros", new
        {
            nome = $"Sim {Guid.NewGuid():N}"[..18], cidade = "São Paulo", uf = "SP", latitude = -23.5, longitude = -46.6, limiarChuvaMm = 20
        })).LerAsync<BairroResponse>();
        var codigo = $"BL-{Guid.NewGuid():N}"[..12].ToUpper();
        var sensor = await (await _admin.PostJsonAsync("/api/v1/sensores", new { codigo, tipo = "BocaDeLobo", bairroId = bairro.Id, limiar = 70 }))
            .LerAsync<SensorResponse>();
        await _admin.PostJsonAsync($"/api/v1/robos", new { codigo = $"DES-{Guid.NewGuid():N}"[..12], tipo = "Desentupidor", bairroBaseId = bairro.Id });
        await _admin.PostJsonAsync($"/api/v1/sensores/{sensor.Id}/leituras", new { valor = 69 }); // quase no limiar

        // Ciclo 1: no temporal o lixo cresce de 4 a 15 pontos → passa de 70% e abre a missão.
        var ciclo1 = await (await _admin.PostAsync("/api/v1/simulador/ciclo", null)).LerAsync<ResultadoCicloSimulacaoResponse>();
        var leitura = Assert.Single(ciclo1.Leituras, l => l.Sensor == codigo);
        Assert.InRange(leitura.Valor, 73m, 84m);
        Assert.Contains(ciclo1.AcoesExecutadas, a => a.Contains(codigo) && a.Contains("atribuída ao robô"));

        // Ciclo 2: a missão (duração 0 s nos testes) é concluída pelo robô.
        var ciclo2 = await (await _admin.PostAsync("/api/v1/simulador/ciclo", null)).LerAsync<ResultadoCicloSimulacaoResponse>();
        Assert.True(ciclo2.Ciclo > ciclo1.Ciclo);
        Assert.Contains(ciclo2.AcoesExecutadas, a => a.Contains("concluiu") && a.Contains(codigo));

        var missoes = await (await _admin.GetAsync($"/api/v1/missoes?bairroId={bairro.Id}")).LerAsync<List<MissaoResponse>>();
        Assert.Equal(StatusMissao.Concluida, Assert.Single(missoes).Status);
    }
}

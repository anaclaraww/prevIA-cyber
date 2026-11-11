using System.Net;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.IntegrationTests.Infra;

namespace GlobalSolutionCitizenTech.IntegrationTests;

/// <summary>
/// Cenário ponta a ponta de uma chuva forte, passando só pela API HTTP:
/// entupimento → robô despachado → risco ALTO → quadra em preparação → quadra retendo e cheia
/// → missão concluída e robô assume a próxima → painel do bairro consolidado.
/// </summary>
[Collection(ApiCollection.Nome)]
public class CenarioChuvaTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.Cliente();

    [Fact]
    public async Task ChuvaForte_SistemaSentePreveEAge()
    {
        // ---------- cadastro ----------
        var bairro = await Criar<BairroResponse>("/api/v1/bairros", new
        {
            nome = $"Pantanal {Guid.NewGuid():N}"[..20], cidade = "São Paulo", uf = "SP",
            latitude = -23.4987, longitude = -46.4416, limiarChuvaMm = 20
        });
        var quadra = await Criar<QuadraResponse>("/api/v1/quadras", new
        {
            nome = "Quadra Poliesportiva", bairroId = bairro.Id, cep = CepControlavel.CepValido,
            latitude = -23.499, longitude = -46.442, capacidadeM3 = 420
        });
        var sufixo = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var bocaDeLobo1 = await Criar<SensorResponse>("/api/v1/sensores", new { codigo = $"BL1-{sufixo}", tipo = "BocaDeLobo", bairroId = bairro.Id, limiar = 70 });
        var bocaDeLobo2 = await Criar<SensorResponse>("/api/v1/sensores", new { codigo = $"BL2-{sufixo}", tipo = "BocaDeLobo", bairroId = bairro.Id, limiar = 70 });
        var nivel = await Criar<SensorResponse>("/api/v1/sensores", new { codigo = $"NIV-{sufixo}", tipo = "NivelQuadra", bairroId = bairro.Id, quadraId = quadra.Id });
        var robo = await Criar<RoboResponse>("/api/v1/robos", new { codigo = $"DES-{sufixo}", tipo = "Desentupidor", bairroBaseId = bairro.Id, bateria = 90 });

        // ---------- 1. sentir: dois entupimentos ----------
        var leitura1 = await Criar<LeituraProcessadaResponse>($"/api/v1/sensores/{bocaDeLobo1.Id}/leituras", new { valor = 85 });
        Assert.Contains(leitura1.AcoesExecutadas, a => a.Contains($"atribuída ao robô DES-{sufixo}"));

        var leitura2 = await Criar<LeituraProcessadaResponse>($"/api/v1/sensores/{bocaDeLobo2.Id}/leituras", new { valor = 92 });
        Assert.Contains(leitura2.AcoesExecutadas, a => a.Contains("aguardando robô"));

        // ---------- 2. prever: 25 mm previstos para um limiar de 20 mm ----------
        factory.Previsao.ChuvaMm = 25;
        var risco = await (await _http.PostAsync($"/api/v1/bairros/{bairro.Id}/avaliar-risco?horas=6", null))
            .LerAsync<ResultadoAvaliacaoRiscoResponse>();
        Assert.Equal(NivelRisco.Alto, risco.Avaliacao.Nivel);
        Assert.Equal(2, risco.Avaliacao.EntupimentosAbertos);

        // ---------- 3. agir: quadra em preparação, missões em prioridade alta, alerta ----------
        var quadraPreparada = await (await _http.GetAsync($"/api/v1/quadras/{quadra.Id}")).LerAsync<QuadraResponse>();
        Assert.Equal(StatusQuadra.EmPreparacao, quadraPreparada.Status);

        var missoes = await (await _http.GetAsync($"/api/v1/missoes?bairroId={bairro.Id}")).LerAsync<List<MissaoResponse>>();
        Assert.Equal(2, missoes.Count);
        Assert.All(missoes, m => Assert.Equal(PrioridadeMissao.Alta, m.Prioridade));

        // ---------- 4. a quadra recebe água e chega perto da capacidade ----------
        await Criar<LeituraProcessadaResponse>($"/api/v1/sensores/{nivel.Id}/leituras", new { valor = 50 });
        var quadraRetendo = await (await _http.GetAsync($"/api/v1/quadras/{quadra.Id}")).LerAsync<QuadraResponse>();
        Assert.Equal(StatusQuadra.Retendo, quadraRetendo.Status);
        Assert.Equal(210m, quadraRetendo.VolumeRetidoM3);

        await Criar<LeituraProcessadaResponse>($"/api/v1/sensores/{nivel.Id}/leituras", new { valor = 93 });
        var alertas = await (await _http.GetAsync($"/api/v1/alertas?bairroId={bairro.Id}&ativo=true")).LerAsync<List<AlertaResponse>>();
        Assert.Equal(2, alertas.Count);                                   // risco do bairro + quadra cheia
        Assert.Contains(alertas, a => a.QuadraId == quadra.Id);

        // ---------- 5. o robô termina a primeira missão e assume a segunda ----------
        var primeira = missoes.Single(m => m.SensorId == bocaDeLobo1.Id);
        var concluir = await _http.PatchJsonAsync($"/api/v1/missoes/{primeira.Id}/concluir");
        Assert.Equal(HttpStatusCode.OK, concluir.StatusCode);
        var concluida = await concluir.LerAsync<MissaoConcluidaResponse>();
        Assert.Equal(StatusMissao.Concluida, concluida.Missao.Status);
        Assert.Equal(bocaDeLobo2.Id, concluida.ProximaMissaoDoRobo?.SensorId);

        // ---------- 6. depois do pico: a água é liberada e a quadra volta ao uso ----------
        Assert.Equal(HttpStatusCode.OK, (await _http.PatchJsonAsync($"/api/v1/quadras/{quadra.Id}/status", new { status = "Esvaziando" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.PatchJsonAsync($"/api/v1/quadras/{quadra.Id}/status", new { status = "Disponivel" })).StatusCode);

        // ---------- painel consolidado ----------
        var painel = await (await _http.GetAsync($"/api/v1/bairros/{bairro.Id}/painel")).LerAsync<PainelBairroResponse>();
        Assert.Equal(NivelRisco.Alto, painel.UltimaAvaliacao?.Nivel);
        Assert.Single(painel.MissoesAbertas);
        Assert.Equal(StatusRobo.EmMissao, painel.Robos.Single().Status);
        Assert.Equal(90 - 15, painel.Robos.Single().Bateria);
        Assert.Equal(StatusQuadra.Disponivel, painel.Quadras.Single().Status);
        Assert.Equal(0m, painel.VolumeTotalRetidoM3);
        Assert.Equal(2, painel.AlertasAtivos.Count);
    }

    private async Task<T> Criar<T>(string url, object corpo)
    {
        var resposta = await _http.PostJsonAsync(url, corpo);
        Assert.True(resposta.StatusCode == HttpStatusCode.Created,
            $"POST {url} devolveu {(int)resposta.StatusCode}: {await resposta.Content.ReadAsStringAsync()}");
        return await resposta.LerAsync<T>();
    }
}

using System.Net;
using System.Net.Http.Json;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.IntegrationTests.Infra;

namespace GlobalSolutionCitizenTech.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class CrudTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.Cliente();

    private async Task<T> Post<T>(string url, object corpo)
    {
        var resposta = await _http.PostJsonAsync(url, corpo);
        Assert.True(resposta.StatusCode == HttpStatusCode.Created,
            $"POST {url} devolveu {(int)resposta.StatusCode}: {await resposta.Content.ReadAsStringAsync()}");
        return await resposta.LerAsync<T>();
    }

    private Task<BairroResponse> NovoBairro() => Post<BairroResponse>("/api/v1/bairros", new
    {
        nome = $"Crud {Guid.NewGuid():N}"[..18], cidade = "São Paulo", uf = "SP", latitude = -23.5, longitude = -46.6, limiarChuvaMm = 20
    });

    private static string Codigo(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}"[..12].ToUpper();

    [Fact]
    public async Task Quadra_ListarAtualizarRemover()
    {
        var bairro = await NovoBairro();
        var quadra = await Post<QuadraResponse>("/api/v1/quadras", new
        {
            nome = "Quadra Crud", bairroId = bairro.Id, cep = CepControlavel.CepValido, latitude = -23.5, longitude = -46.6, capacidadeM3 = 300
        });

        var lista = await (await _http.GetAsync($"/api/v1/quadras?bairroId={bairro.Id}&status=Disponivel")).LerAsync<List<QuadraResponse>>();
        Assert.Single(lista);

        var atualizada = await (await _http.PutAsJsonAsync($"/api/v1/quadras/{quadra.Id}",
            new { nome = "Quadra Renomeada", numero = "42", capacidadeM3 = 350 }, ApiFactory.Json)).LerAsync<QuadraResponse>();
        Assert.Equal("Quadra Renomeada", atualizada.Nome);
        Assert.Equal(350m, atualizada.CapacidadeM3);

        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/v1/quadras/{quadra.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/quadras/{quadra.Id}")).StatusCode);
    }

    [Fact]
    public async Task Quadra_EmOperacao_NaoPodeSerRemovida()
    {
        var bairro = await NovoBairro();
        var quadra = await Post<QuadraResponse>("/api/v1/quadras", new
        {
            nome = "Quadra Operando", bairroId = bairro.Id, cep = CepControlavel.CepValido, latitude = -23.5, longitude = -46.6, capacidadeM3 = 300
        });
        await _http.PatchJsonAsync($"/api/v1/quadras/{quadra.Id}/status", new { status = "EmPreparacao" });

        Assert.Equal(HttpStatusCode.Conflict, (await _http.DeleteAsync($"/api/v1/quadras/{quadra.Id}")).StatusCode);
    }

    [Fact]
    public async Task Robo_ManutencaoRecarregarRemover()
    {
        var bairro = await NovoBairro();
        var robo = await Post<RoboResponse>("/api/v1/robos", new { codigo = Codigo("DES"), tipo = "Desentupidor", bairroBaseId = bairro.Id, bateria = 40 });

        var lista = await (await _http.GetAsync($"/api/v1/robos?bairroId={bairro.Id}&status=Disponivel")).LerAsync<List<RoboResponse>>();
        Assert.Single(lista);

        var manutencao = await (await _http.PostAsync($"/api/v1/robos/{robo.Id}/manutencao", null)).LerAsync<RoboResponse>();
        Assert.Equal(StatusRobo.Manutencao, manutencao.Status);

        var recarregado = await (await _http.PostAsync($"/api/v1/robos/{robo.Id}/recarregar", null)).LerAsync<RoboResponse>();
        Assert.Equal(100, recarregado.Bateria);
        Assert.Equal(StatusRobo.Disponivel, recarregado.Status);

        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/v1/robos/{robo.Id}")).StatusCode);
    }

    [Fact]
    public async Task Robo_ComHistoricoDeMissao_NaoPodeSerRemovido()
    {
        var bairro = await NovoBairro();
        var robo = await Post<RoboResponse>("/api/v1/robos", new { codigo = Codigo("INS"), tipo = "Inspetor", bairroBaseId = bairro.Id });
        var missao = await Post<MissaoResponse>("/api/v1/missoes", new { tipo = "Inspecao", bairroId = bairro.Id, descricao = "Inspeção da galeria principal" });
        Assert.Equal(robo.Id, missao.RoboId);

        Assert.Equal(HttpStatusCode.Conflict, (await _http.DeleteAsync($"/api/v1/robos/{robo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsync($"/api/v1/robos/{robo.Id}/manutencao", null)).StatusCode);

        var obtida = await (await _http.GetAsync($"/api/v1/missoes/{missao.Id}")).LerAsync<MissaoResponse>();
        Assert.Equal(StatusMissao.EmAndamento, obtida.Status);
        var emAndamento = await (await _http.GetAsync($"/api/v1/missoes?status=EmAndamento&bairroId={bairro.Id}")).LerAsync<List<MissaoResponse>>();
        Assert.Single(emAndamento);
    }

    [Fact]
    public async Task Missao_ConcluirPendente_409()
    {
        var bairro = await NovoBairro();
        var missao = await Post<MissaoResponse>("/api/v1/missoes", new { tipo = "Desobstrucao", bairroId = bairro.Id });
        Assert.Equal(StatusMissao.Pendente, missao.Status);

        Assert.Equal(HttpStatusCode.Conflict, (await _http.PatchJsonAsync($"/api/v1/missoes/{missao.Id}/concluir")).StatusCode);
    }

    [Fact]
    public async Task Alerta_ListarEEncerrar()
    {
        var bairro = await NovoBairro();
        factory.Previsao.ChuvaMm = 12; // risco médio
        await _http.PostAsync($"/api/v1/bairros/{bairro.Id}/avaliar-risco", null);

        var alertas = await (await _http.GetAsync($"/api/v1/alertas?bairroId={bairro.Id}&ativo=true")).LerAsync<List<AlertaResponse>>();
        var alerta = Assert.Single(alertas);
        Assert.Equal(NivelRisco.Medio, alerta.Nivel);

        var encerrado = await (await _http.PatchJsonAsync($"/api/v1/alertas/{alerta.Id}/encerrar")).LerAsync<AlertaResponse>();
        Assert.False(encerrado.Ativo);
        Assert.NotNull(encerrado.EncerradoEm);
        Assert.Equal(HttpStatusCode.Conflict, (await _http.PatchJsonAsync($"/api/v1/alertas/{alerta.Id}/encerrar")).StatusCode);

        var historico = await (await _http.GetAsync($"/api/v1/bairros/{bairro.Id}/avaliacoes-risco")).LerAsync<List<AvaliacaoRiscoResponse>>();
        Assert.Single(historico);
    }

    [Fact]
    public async Task Sensores_ListarObterELeituras()
    {
        var bairro = await NovoBairro();
        var sensor = await Post<SensorResponse>("/api/v1/sensores", new { codigo = Codigo("PLU"), tipo = "Pluviometro", bairroId = bairro.Id });
        await Post<LeituraProcessadaResponse>($"/api/v1/sensores/{sensor.Id}/leituras", new { valor = 3.5 });
        await Post<LeituraProcessadaResponse>($"/api/v1/sensores/{sensor.Id}/leituras", new { valor = 7.2 });

        var lista = await (await _http.GetAsync($"/api/v1/sensores?bairroId={bairro.Id}&tipo=Pluviometro")).LerAsync<List<SensorResponse>>();
        Assert.Single(lista);

        var obtido = await (await _http.GetAsync($"/api/v1/sensores/{sensor.Id}")).LerAsync<SensorResponse>();
        Assert.Equal(7.2m, obtido.UltimoValor);
        Assert.Equal("mm/h", obtido.Unidade);

        var leituras = await (await _http.GetAsync($"/api/v1/sensores/{sensor.Id}/leituras?pagina=1&tamanhoPagina=1")).LerAsync<PaginaResponse<LeituraResponse>>();
        Assert.Equal(2, leituras.Total);
        Assert.Equal(7.2m, Assert.Single(leituras.Itens).Valor);
    }

    [Fact]
    public async Task Sensor_NivelQuadraDeOutroBairro_409()
    {
        var bairroA = await NovoBairro();
        var bairroB = await NovoBairro();
        var quadraA = await Post<QuadraResponse>("/api/v1/quadras", new
        {
            nome = "Quadra A", bairroId = bairroA.Id, cep = CepControlavel.CepValido, latitude = -23.5, longitude = -46.6, capacidadeM3 = 300
        });

        var resposta = await _http.PostJsonAsync("/api/v1/sensores", new
        {
            codigo = Codigo("NIV"), tipo = "NivelQuadra", bairroId = bairroB.Id, quadraId = quadraA.Id
        });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }
}

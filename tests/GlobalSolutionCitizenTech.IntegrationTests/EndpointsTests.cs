using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.IntegrationTests.Infra;

namespace GlobalSolutionCitizenTech.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class EndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.Cliente();

    private async Task<BairroResponse> CriarBairroAsync(decimal limiar = 20m)
    {
        var resposta = await _http.PostJsonAsync("/api/v1/bairros", new
        {
            nome = $"Bairro {Guid.NewGuid():N}"[..20], cidade = "São Paulo", uf = "SP",
            latitude = -23.5, longitude = -46.6, limiarChuvaMm = limiar
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return await resposta.LerAsync<BairroResponse>();
    }

    private async Task<QuadraResponse> CriarQuadraAsync(Guid bairroId)
    {
        var resposta = await _http.PostJsonAsync("/api/v1/quadras", new
        {
            nome = "Quadra Integração", bairroId, cep = "01310-100", numero = "1000",
            latitude = -23.56, longitude = -46.65, capacidadeM3 = 400
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return await resposta.LerAsync<QuadraResponse>();
    }

    [Fact]
    public async Task Bairro_CriarObterAtualizarRemover()
    {
        var bairro = await CriarBairroAsync();

        var obtido = await (await _http.GetAsync($"/api/v1/bairros/{bairro.Id}")).LerAsync<BairroResponse>();
        Assert.Equal(bairro.Nome, obtido.Nome);

        var atualizar = await _http.PutAsJsonAsync($"/api/v1/bairros/{bairro.Id}", new
        {
            nome = bairro.Nome, cidade = "São Paulo", uf = "sp", latitude = -23.5, longitude = -46.6, limiarChuvaMm = 35
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, atualizar.StatusCode);
        Assert.Equal(35m, (await atualizar.LerAsync<BairroResponse>()).LimiarChuvaMm);

        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/v1/bairros/{bairro.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/bairros/{bairro.Id}")).StatusCode);
    }

    [Fact]
    public async Task Bairro_PayloadInvalido_400ComEnvelopePadrao()
    {
        var resposta = await _http.PostJsonAsync("/api/v1/bairros", new
        {
            nome = "", cidade = "SP", uf = "SPX", latitude = -23, longitude = -46, limiarChuvaMm = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("errorCode").GetString());
        var detalhes = json.RootElement.GetProperty("details");
        Assert.True(detalhes.TryGetProperty("Uf", out _));
        Assert.True(detalhes.TryGetProperty("LimiarChuvaMm", out _));
    }

    [Fact]
    public async Task Bairro_ComQuadra_NaoPodeSerRemovido_409()
    {
        var bairro = await CriarBairroAsync();
        await CriarQuadraAsync(bairro.Id);

        var resposta = await _http.DeleteAsync($"/api/v1/bairros/{bairro.Id}");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task RecursoInexistente_404()
    {
        var resposta = await _http.GetAsync($"/api/v1/robos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Contains("RESOURCE_NOT_FOUND", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Quadra_LogradouroVemDoCep()
    {
        var bairro = await CriarBairroAsync();

        var quadra = await CriarQuadraAsync(bairro.Id);

        Assert.Equal("Avenida Paulista", quadra.Logradouro);
        Assert.Equal("01310100", quadra.Cep);
        Assert.Equal(StatusQuadra.Disponivel, quadra.Status);
        Assert.Equal([StatusQuadra.EmPreparacao, StatusQuadra.Manutencao], quadra.ProximosStatusPermitidos);
    }

    [Fact]
    public async Task Quadra_CepInexistente_400()
    {
        var bairro = await CriarBairroAsync();

        var resposta = await _http.PostJsonAsync("/api/v1/quadras", new
        {
            nome = "Quadra X", bairroId = bairro.Id, cep = "99999-999", latitude = -23.5, longitude = -46.6, capacidadeM3 = 100
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("CEP", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Quadra_TransicaoInvalida_409()
    {
        var bairro = await CriarBairroAsync();
        var quadra = await CriarQuadraAsync(bairro.Id);

        var resposta = await _http.PatchJsonAsync($"/api/v1/quadras/{quadra.Id}/status", new { status = "Esvaziando" });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Contains("BUSINESS_RULE_VIOLATION", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sensor_CodigoDuplicado_409()
    {
        var bairro = await CriarBairroAsync();
        var codigo = $"PLU-{Guid.NewGuid():N}"[..12];
        var corpo = new { codigo, tipo = "Pluviometro", bairroId = bairro.Id };

        Assert.Equal(HttpStatusCode.Created, (await _http.PostJsonAsync("/api/v1/sensores", corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _http.PostJsonAsync("/api/v1/sensores", corpo)).StatusCode);
    }

    [Fact]
    public async Task Sensor_Inativo_RecusaLeitura_409()
    {
        var bairro = await CriarBairroAsync();
        var sensor = await (await _http.PostJsonAsync("/api/v1/sensores", new
        {
            codigo = $"PLU-{Guid.NewGuid():N}"[..12], tipo = "Pluviometro", bairroId = bairro.Id
        })).LerAsync<SensorResponse>();
        await _http.PatchJsonAsync($"/api/v1/sensores/{sensor.Id}/ativacao", new { ativo = false });

        var resposta = await _http.PostJsonAsync($"/api/v1/sensores/{sensor.Id}/leituras", new { valor = 12 });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task AvaliarRisco_PrevisaoIndisponivel_502()
    {
        var bairro = await CriarBairroAsync();
        factory.Previsao.Falhar = true;
        try
        {
            var resposta = await _http.PostAsync($"/api/v1/bairros/{bairro.Id}/avaliar-risco", null);

            Assert.Equal(HttpStatusCode.BadGateway, resposta.StatusCode);
            Assert.Contains("EXTERNAL_API_ERROR", await resposta.Content.ReadAsStringAsync());
        }
        finally
        {
            factory.Previsao.Falhar = false;
        }
    }

    [Fact]
    public async Task AvaliarRisco_HorasForaDaFaixa_400()
    {
        var bairro = await CriarBairroAsync();

        var resposta = await _http.PostAsync($"/api/v1/bairros/{bairro.Id}/avaliar-risco?horas=100", null);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Swagger_DocumentaOsEndpoints()
    {
        var resposta = await _http.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/sensores/{id}/leituras", json);
        Assert.Contains("/api/v1/bairros/{id}/avaliar-risco", json);
    }

    [Fact]
    public async Task Health_PostgresAcessivel()
    {
        var resposta = await _http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }
}

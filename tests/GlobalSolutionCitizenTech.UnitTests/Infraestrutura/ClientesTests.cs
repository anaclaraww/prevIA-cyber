using System.Net;
using System.Text;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Infrastructure.Clientes;

namespace GlobalSolutionCitizenTech.UnitTests.Infraestrutura;

public class ClientesTests
{
    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ultima = request;
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Json(string corpo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private static HttpClient Http(HandlerFalso handler, string baseUrl) => new(handler) { BaseAddress = new Uri(baseUrl) };

    [Fact]
    public async Task OpenMeteo_SomaPrecipitacaoDasProximasHoras()
    {
        var handler = new HandlerFalso(_ => Json("""
            {"hourly":{"time":["t1","t2","t3"],"precipitation":[1.2,0.0,3.35]}}
            """));
        var client = new OpenMeteoClient(Http(handler, "https://api.open-meteo.com/"));

        var chuva = await client.ObterChuvaPrevistaMmAsync(-23.5, -46.6, 3, CancellationToken.None);

        Assert.Equal(4.55m, chuva);
        var url = handler.Ultima!.RequestUri!.ToString();
        Assert.Contains("latitude=-23.5", url);          // ponto decimal, independente da cultura
        Assert.Contains("forecast_hours=3", url);
        Assert.Contains("hourly=precipitation", url);
    }

    [Fact]
    public async Task OpenMeteo_ValoresNulosContamComoZero()
    {
        var handler = new HandlerFalso(_ => Json("""{"hourly":{"time":["t1","t2"],"precipitation":[null,2.0]}}"""));
        var client = new OpenMeteoClient(Http(handler, "https://api.open-meteo.com/"));

        Assert.Equal(2m, await client.ObterChuvaPrevistaMmAsync(0, 0, 2, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "{}")]                 // sem 'hourly'
    [InlineData(HttpStatusCode.OK, "isto não é json")]
    public async Task OpenMeteo_FalhaViraServicoExterno(HttpStatusCode status, string corpo)
    {
        var client = new OpenMeteoClient(Http(new HandlerFalso(_ => Json(corpo, status)), "https://api.open-meteo.com/"));

        var ex = await Assert.ThrowsAsync<ServicoExternoException>(() =>
            client.ObterChuvaPrevistaMmAsync(0, 0, 6, CancellationToken.None));
        Assert.Equal("Open-Meteo", ex.Servico);
    }

    [Fact]
    public async Task ViaCep_CepEncontrado_RetornaEndereco()
    {
        var handler = new HandlerFalso(_ => Json("""
            {"cep":"01310-100","logradouro":"Avenida Paulista","bairro":"Bela Vista","localidade":"São Paulo","uf":"SP"}
            """));
        var client = new ViaCepClient(Http(handler, "https://viacep.com.br/"));

        var endereco = await client.ConsultarAsync("01310100", CancellationToken.None);

        Assert.NotNull(endereco);
        Assert.Equal("Avenida Paulista", endereco.Logradouro);
        Assert.Equal("São Paulo", endereco.Cidade);
        Assert.EndsWith("ws/01310100/json/", handler.Ultima!.RequestUri!.ToString());
    }

    [Theory]
    [InlineData("""{"erro": true}""")]
    [InlineData("""{"erro": "true"}""")]
    public async Task ViaCep_CepInexistente_RetornaNull(string corpo)
    {
        var client = new ViaCepClient(Http(new HandlerFalso(_ => Json(corpo)), "https://viacep.com.br/"));

        Assert.Null(await client.ConsultarAsync("99999999", CancellationToken.None));
    }

    [Fact]
    public async Task ViaCep_FormatoInvalido_RetornaNull()
    {
        var client = new ViaCepClient(Http(new HandlerFalso(_ => Json("", HttpStatusCode.BadRequest)), "https://viacep.com.br/"));

        Assert.Null(await client.ConsultarAsync("123", CancellationToken.None));
    }

    [Fact]
    public async Task ViaCep_ServidorFora_ViraServicoExterno()
    {
        var client = new ViaCepClient(Http(new HandlerFalso(_ => Json("", HttpStatusCode.ServiceUnavailable)), "https://viacep.com.br/"));

        var ex = await Assert.ThrowsAsync<ServicoExternoException>(() => client.ConsultarAsync("01310100", CancellationToken.None));
        Assert.Equal("ViaCEP", ex.Servico);
    }
}

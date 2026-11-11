using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.IntegrationTests.Infra;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GlobalSolutionCitizenTech.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class AutenticacaoTests(ApiFactory factory)
{
    private readonly HttpClient _anonimo = factory.CreateClient();

    private static async Task<string> CodigoErroAsync(HttpResponseMessage resposta)
    {
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errorCode").GetString()!;
    }

    private static object NovoBairro() => new
    {
        nome = $"Auth {Guid.NewGuid():N}"[..18], cidade = "São Paulo", uf = "SP", latitude = -23.5, longitude = -46.6, limiarChuvaMm = 20
    };

    [Theory]
    [InlineData("Admin")]
    [InlineData("Operador")]
    [InlineData("Sensor")]
    public async Task Token_CredenciaisValidas_EmiteJwtComPerfil(string perfil)
    {
        var (id, segredo) = ApiFactory.Credenciais[perfil];

        var resposta = await _anonimo.PostAsJsonAsync("/api/v1/auth/token", new { clientId = id, clientSecret = segredo });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Assert.Equal("Bearer", json.RootElement.GetProperty("tokenType").GetString());
        Assert.Equal(perfil, json.RootElement.GetProperty("perfil").GetString());
        Assert.Equal(3600, json.RootElement.GetProperty("expiresIn").GetInt32());

        var jwt = new JsonWebToken(json.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal(id, jwt.Subject);
        Assert.Contains(jwt.Claims, c => c.Value == perfil);
    }

    [Theory]
    [InlineData("admin-teste", "senha-errada")]
    [InlineData("cliente-inexistente", "qualquer")]
    [InlineData("admin-teste", "")]
    public async Task Token_CredenciaisInvalidas_401(string clientId, string clientSecret)
    {
        var resposta = await _anonimo.PostAsJsonAsync("/api/v1/auth/token", new { clientId, clientSecret });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("UNAUTHORIZED_ACCESS", await CodigoErroAsync(resposta));
    }

    [Fact]
    public async Task SemToken_401ComEnvelopePadrao()
    {
        var resposta = await _anonimo.GetAsync("/api/v1/bairros");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("UNAUTHORIZED_ACCESS", await CodigoErroAsync(resposta));
    }

    [Fact]
    public async Task TokenMalformado_401()
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "isto.nao.e-um-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/bairros")).StatusCode);
    }

    [Fact]
    public async Task TokenAssinadoComOutraChave_401()
    {
        var falso = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "GlobalSolutionCitizenTech",
            Audience = "citizentech-api",
            Expires = DateTime.UtcNow.AddMinutes(10),
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin"), new Claim("sub", "intruso")]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("outra-chave-qualquer-com-mais-de-32-caracteres")),
                SecurityAlgorithms.HmacSha256),
        });
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", falso);

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/bairros")).StatusCode);
    }

    [Fact]
    public async Task TokenExpirado_401()
    {
        var expirado = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "GlobalSolutionCitizenTech",
            Audience = "citizentech-api",
            NotBefore = DateTime.UtcNow.AddHours(-2),
            IssuedAt = DateTime.UtcNow.AddHours(-2),
            Expires = DateTime.UtcNow.AddHours(-1),
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin"), new Claim("sub", "admin-teste")]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.ChaveJwt)), SecurityAlgorithms.HmacSha256),
        });
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expirado);

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/bairros")).StatusCode);
    }

    [Fact]
    public async Task EndpointsPublicos_SemToken()
    {
        Assert.Equal(HttpStatusCode.OK, (await _anonimo.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _anonimo.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Fact]
    public async Task PerfilSensor_SoEnviaLeituras()
    {
        var admin = factory.Cliente("Admin");
        var sensor = factory.Cliente("Sensor");
        var bairro = await (await admin.PostJsonAsync("/api/v1/bairros", NovoBairro())).LerAsync<BairroResponse>();
        var pluviometro = await (await admin.PostJsonAsync("/api/v1/sensores", new
        {
            codigo = $"PLU-{Guid.NewGuid():N}"[..12], tipo = "Pluviometro", bairroId = bairro.Id
        })).LerAsync<SensorResponse>();

        Assert.Equal(HttpStatusCode.Created, (await sensor.PostJsonAsync($"/api/v1/sensores/{pluviometro.Id}/leituras", new { valor = 4.2 })).StatusCode);

        var consulta = await sensor.GetAsync("/api/v1/bairros");
        Assert.Equal(HttpStatusCode.Forbidden, consulta.StatusCode);
        Assert.Equal("FORBIDDEN", await CodigoErroAsync(consulta));
        Assert.Equal(HttpStatusCode.Forbidden, (await sensor.PostJsonAsync("/api/v1/bairros", NovoBairro())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sensor.PostAsync($"/api/v1/bairros/{bairro.Id}/avaliar-risco", null)).StatusCode);
    }

    [Fact]
    public async Task PerfilOperador_ConsultaEOperaMasNaoCadastra()
    {
        var admin = factory.Cliente("Admin");
        var operador = factory.Cliente("Operador");
        var bairro = await (await admin.PostJsonAsync("/api/v1/bairros", NovoBairro())).LerAsync<BairroResponse>();
        factory.Previsao.ChuvaMm = 1;

        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync($"/api/v1/bairros/{bairro.Id}/painel")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await operador.PostAsync($"/api/v1/bairros/{bairro.Id}/avaliar-risco", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostJsonAsync("/api/v1/bairros", NovoBairro())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.DeleteAsync($"/api/v1/bairros/{bairro.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostAsync("/api/v1/simulador/ciclo", null)).StatusCode);
    }

    [Fact]
    public async Task Swagger_DeclaraEsquemaBearer()
    {
        var json = await _anonimo.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("\"bearer\"", json);
        Assert.Contains("/api/v1/auth/token", json);
    }
}

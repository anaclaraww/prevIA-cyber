using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GlobalSolutionCitizenTech.IntegrationTests.Infra;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GlobalSolutionCitizenTech.IntegrationTests;

/// <summary>Endurecimento da API para rodar exposta: cabeçalhos, rate limit, teto de corpo, Swagger e JWT.</summary>
[Collection(ApiCollection.Nome)]
public class SegurancaApiTests(ApiFactory factory)
{
    private static async Task<JsonElement> ErroAsync(HttpResponseMessage resposta) =>
        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Mesmo banco do fixture, com outra configuração de segurança.</summary>
    private WebApplicationFactory<Program> Host(params (string Chave, string Valor)[] ajustes) =>
        factory.WithWebHostBuilder(b =>
        {
            foreach (var (chave, valor) in ajustes)
                b.UseSetting(chave, valor);
        });

    [Theory]
    [InlineData("/api/v1/bairros")]
    [InlineData("/painel/")]
    [InlineData("/api/v1/nao-existe")]
    public async Task Toda_resposta_sai_com_cabecalhos_de_seguranca(string caminho)
    {
        var resposta = await factory.Cliente("Operador").GetAsync(caminho);

        var h = resposta.Headers;
        Assert.Equal("nosniff", h.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", h.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", h.GetValues("Referrer-Policy").Single());
        Assert.Equal("same-origin", h.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", h.GetValues("Content-Security-Policy").Single());
        Assert.DoesNotContain("unsafe-inline", h.GetValues("Content-Security-Policy").Single());
        Assert.False(h.Contains("X-XSS-Protection"));
    }

    [Fact]
    public async Task Resposta_da_api_nao_vai_para_cache()
    {
        var resposta = await factory.Cliente("Operador").GetAsync("/api/v1/bairros");

        Assert.True(resposta.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Login_acima_do_limite_por_minuto_vira_429()
    {
        await using var host = Host(("Seguranca:RateLimit:LoginPorMinuto", "3"));
        var http = host.CreateClient();
        var corpo = new { clientId = "admin-teste", clientSecret = "errado" };

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/v1/auth/token", corpo)).StatusCode);

        // Nem a senha certa passa enquanto a janela não vira: é isso que trava a força bruta.
        var (id, segredo) = ApiFactory.Credenciais["Admin"];
        var bloqueada = await http.PostAsJsonAsync("/api/v1/auth/token", new { clientId = id, clientSecret = segredo });

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.Equal("TOO_MANY_REQUESTS", (await ErroAsync(bloqueada)).GetProperty("errorCode").GetString());
        Assert.True(bloqueada.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal("nosniff", bloqueada.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Requisicoes_acima_do_limite_geral_viram_429_mas_health_continua()
    {
        await using var host = Host(("Seguranca:RateLimit:RequisicoesPorMinuto", "5"));
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token("Operador"));

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/v1/bairros")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await http.GetAsync("/api/v1/bairros")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Corpo_acima_do_limite_vira_413_antes_de_chegar_ao_controller()
    {
        // Acima de 1 MB o motor de PII não varre o corpo: deixar passar seria um desvio do taint analysis.
        var json = JsonSerializer.Serialize(new { nome = "Teste", cpf = "090.254.552-39", observacao = new string('a', 1024 * 1024) });
        var resposta = await factory.Cliente("Operador").PostAsync("/api/v1/moradores",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resposta.StatusCode);
        Assert.Equal("PAYLOAD_TOO_LARGE", (await ErroAsync(resposta)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Sem_ExporSwagger_a_documentacao_nao_e_publicada()
    {
        await using var host = Host(("Seguranca:ExporSwagger", "false"));
        var http = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Sem a rota, a política padrão (exige token) responde antes do 404: o importante é não sair o documento.
        var swagger = await http.GetAsync("/swagger/v1/swagger.json");
        Assert.Contains(swagger.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });
        var raiz = await http.GetAsync("/");
        Assert.Equal("/painel/", raiz.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Token_com_outro_algoritmo_e_recusado_mesmo_com_a_chave_certa()
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.ChaveJwt));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "GlobalSolutionCitizenTech",
            Audience = "citizentech-api",
            Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = new ClaimsIdentity([new Claim("sub", "admin-teste"), new Claim(ClaimTypes.Role, "Admin")]),
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha384),
        });

        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/bairros")).StatusCode);
    }

    [Fact]
    public async Task Token_sem_assinatura_alg_none_e_recusado()
    {
        static string B64(string json) => Base64UrlEncoder.Encode(json);
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var token = B64("""{"alg":"none","typ":"JWT"}""") + "." +
                    B64($$"""{"sub":"admin-teste","role":"Admin","iss":"GlobalSolutionCitizenTech","aud":"citizentech-api","exp":{{exp}}}""") + ".";

        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/bairros")).StatusCode);
    }

    [Fact]
    public async Task Resposta_403_nao_revela_o_perfil_do_token()
    {
        var resposta = await factory.Cliente("Sensor").GetAsync("/api/v1/bairros");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ErroAsync(resposta)).GetProperty("details").ValueKind);
    }
}

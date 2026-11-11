using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Excecoes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace GlobalSolutionCitizenTech.IntegrationTests.Infra;

/// <summary>
/// Sobe a API inteira contra um PostgreSQL real em container (Testcontainers), com as migrations
/// aplicadas no startup. As APIs externas (Open-Meteo e ViaCEP) são trocadas por versões controláveis.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public PrevisaoControlavel Previsao { get; } = new();
    public CepControlavel Cep { get; } = new();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Credenciais de teste por perfil (Admin, Operador, Sensor).</summary>
    public static readonly Dictionary<string, (string ClientId, string Secret)> Credenciais = new()
    {
        ["Admin"] = ("admin-teste", "segredo-admin-teste"),
        ["Operador"] = ("operador-teste", "segredo-operador-teste"),
        ["Sensor"] = ("sensor-teste", "segredo-sensor-teste"),
    };

    public const string ChaveJwt = "chave-de-teste-com-mais-de-32-caracteres-para-hmac";

    private readonly Dictionary<string, string> _tokens = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Database:AplicarMigrations", "true");
        builder.UseSetting("Database:Seed", "false");

        builder.UseSetting("Jwt:SecretKey", ChaveJwt);
        var i = 0;
        foreach (var (perfil, (id, segredo)) in Credenciais)
        {
            builder.UseSetting($"Auth:Clientes:{i}:ClientId", id);
            builder.UseSetting($"Auth:Clientes:{i}:ClientSecret", segredo);
            builder.UseSetting($"Auth:Clientes:{i}:Perfil", perfil);
            i++;
        }

        // Simulador determinístico: cenário de temporal, missões concluídas no ciclo seguinte, sem avaliar risco.
        builder.UseSetting("Simulador:Habilitado", "false");
        builder.UseSetting("Simulador:Cenario", "ChuvaForte");
        builder.UseSetting("Simulador:DuracaoMissaoSegundos", "0");
        builder.UseSetting("Simulador:AvaliarRiscoACadaCiclos", "0");
        builder.UseSetting("Simulador:Semente", "42");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPrevisaoTempoClient>();
            services.RemoveAll<ICepClient>();
            services.AddSingleton<IPrevisaoTempoClient>(Previsao);
            services.AddSingleton<ICepClient>(Cep);
        });
    }

    /// <summary>Cliente HTTP já autenticado com o token do perfil.</summary>
    public HttpClient Cliente(string perfil = "Admin")
    {
        var http = CreateClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token(perfil));
        return http;
    }

    public string Token(string perfil)
    {
        lock (_tokens)
        {
            if (_tokens.TryGetValue(perfil, out var cache))
                return cache;

            var (id, segredo) = Credenciais[perfil];
            var resposta = CreateClient()
                .PostAsJsonAsync("/api/v1/auth/token", new { clientId = id, clientSecret = segredo })
                .GetAwaiter().GetResult();
            resposta.EnsureSuccessStatusCode();
            var token = resposta.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult()
                .GetProperty("accessToken").GetString()!;
            return _tokens[perfil] = token;
        }
    }
}

public sealed class PrevisaoControlavel : IPrevisaoTempoClient
{
    public decimal ChuvaMm { get; set; }
    public bool Falhar { get; set; }

    public Task<decimal> ObterChuvaPrevistaMmAsync(double latitude, double longitude, int horas, CancellationToken ct) =>
        Falhar
            ? throw new ServicoExternoException("Open-Meteo", "indisponível (simulado).")
            : Task.FromResult(ChuvaMm);
}

public sealed class CepControlavel : ICepClient
{
    public const string CepValido = "01310100";

    public Task<EnderecoCep?> ConsultarAsync(string cep, CancellationToken ct) =>
        Task.FromResult(cep == CepValido
            ? new EnderecoCep("01310-100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP")
            : null);
}

[CollectionDefinition(Nome)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Nome = "api";
}

public static class HttpExtensions
{
    public static async Task<T> LerAsync<T>(this HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient http, string url, object corpo) =>
        http.PostAsJsonAsync(url, corpo, ApiFactory.Json);

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient http, string url, object? corpo = null) =>
        http.PatchAsJsonAsync(url, corpo ?? new { }, ApiFactory.Json);
}

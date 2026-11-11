using System.Globalization;
using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Simulacao;
using GlobalSolutionCitizenTech.Infrastructure.Clientes;
using GlobalSolutionCitizenTech.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalSolutionCitizenTech.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // APIs externas: retry, circuit breaker e timeout (Polly via Microsoft.Extensions.Http.Resilience).
        services.AddHttpClient<IPrevisaoTempoClient, OpenMeteoClient>(c =>
                c.BaseAddress = new Uri(configuration["ApisExternas:OpenMeteoBaseUrl"] ?? "https://api.open-meteo.com/"))
            .AddStandardResilienceHandler();

        services.AddHttpClient<ICepClient, ViaCepClient>(c =>
                c.BaseAddress = new Uri(configuration["ApisExternas:ViaCepBaseUrl"] ?? "https://viacep.com.br/"))
            .AddStandardResilienceHandler();

        // Demonstração: uma previsão fixa no lugar do Open-Meteo (ex.: forçar risco alto numa apresentação).
        if (decimal.TryParse(configuration["Simulador:ChuvaPrevistaMm"], NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            services.AddScoped<IPrevisaoTempoClient, PrevisaoSimuladaClient>();

        return services;
    }
}

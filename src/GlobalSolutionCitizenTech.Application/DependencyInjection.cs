using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Application.Simulacao;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalSolutionCitizenTech.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDespachoMissoes, DespachoMissoes>();
        services.AddScoped<IBairroService, BairroService>();
        services.AddScoped<IQuadraService, QuadraService>();
        services.AddScoped<ISensorService, SensorService>();
        services.AddScoped<IRoboService, RoboService>();
        services.AddScoped<IMissaoService, MissaoService>();
        services.AddScoped<IRiscoService, RiscoService>();
        services.AddScoped<IAlertaService, AlertaService>();
        services.AddScoped<IPainelService, PainelService>();

        services.Configure<ConfiguracaoSimulador>(configuration.GetSection(ConfiguracaoSimulador.Secao));
        services.AddSingleton<EstadoSimulacao>();
        services.AddScoped<ISimuladorSensores, SimuladorSensores>();
        return services;
    }
}

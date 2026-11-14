using Anthropic;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Privacidade.Eventos;
using GlobalSolutionCitizenTech.Privacidade.Ia;
using GlobalSolutionCitizenTech.Privacidade.Logging;
using GlobalSolutionCitizenTech.Privacidade.Middlewares;
using GlobalSolutionCitizenTech.Privacidade.Pii;
using GlobalSolutionCitizenTech.Privacidade.Protecao;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Privacidade;

public static class DependencyInjection
{
    /// <summary>Motor de PII, cifra, guarda de log, fila de eventos e tradução via IA.</summary>
    public static IServiceCollection AddPrivacidade(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConfiguracaoPrivacidade>(configuration.GetSection(ConfiguracaoPrivacidade.Secao));

        services.AddSingleton(sp => new MotorPii(sp.GetRequiredService<IOptions<ConfiguracaoPrivacidade>>()));
        services.AddSingleton<FilaEventosSeguranca>();
        services.AddSingleton<IProtetorDados, ProtetorDadosAes>();
        services.AddHostedService<ProcessadorEventosSeguranca>();

        services.AddSingleton<TradutorRiscoOffline>();
        services.AddScoped<ITradutorRisco>(sp =>
        {
            var chave = sp.GetRequiredService<IOptions<ConfiguracaoPrivacidade>>().Value.Ia.ApiKey;
            return string.IsNullOrWhiteSpace(chave)
                ? sp.GetRequiredService<TradutorRiscoOffline>()
                : ActivatorUtilities.CreateInstance<TradutorRiscoClaude>(sp, sp.GetRequiredService<AnthropicClient>());
        });
        services.AddSingleton(sp => new AnthropicClient
        {
            ApiKey = sp.GetRequiredService<IOptions<ConfiguracaoPrivacidade>>().Value.Ia.ApiKey ?? ""
        });

        // Toda ILogger da aplicação (e de qualquer provider registrado depois, inclusive em testes) passa
        // pela barreira de taint.
        services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(sp => new FabricaLoggerComTaint(
            new LoggerFactory(
                sp.GetServices<ILoggerProvider>(),
                sp.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>(),
                sp.GetService<IOptions<LoggerFactoryOptions>>(),
                sp.GetService<IExternalScopeProvider>()),
            sp.GetRequiredService<MotorPii>(),
            sp.GetRequiredService<FilaEventosSeguranca>())));

        return services;
    }

    /// <summary>
    /// Sink HTTP (mais externo) e source. Chamar antes do tratamento de erros, para que o envelope de erro
    /// também seja inspecionado.
    /// </summary>
    public static IApplicationBuilder UsePrivacidade(this IApplicationBuilder app)
    {
        // Falha no startup, e não no primeiro cadastro, se as chaves estiverem ausentes ou erradas.
        _ = app.ApplicationServices.GetRequiredService<IProtetorDados>();

        app.UseMiddleware<RespostaSeguraMiddleware>();
        app.UseMiddleware<FontePiiMiddleware>();
        return app;
    }
}

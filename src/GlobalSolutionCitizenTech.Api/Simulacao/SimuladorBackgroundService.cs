using GlobalSolutionCitizenTech.Application.Simulacao;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Api.Simulacao;

/// <summary>Roda um ciclo do simulador a cada <c>Simulador:IntervaloSegundos</c>, se <c>Simulador:Habilitado</c>.</summary>
public class SimuladorBackgroundService(
    IServiceScopeFactory scopes,
    IOptions<ConfiguracaoSimulador> config,
    ILogger<SimuladorBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = config.Value;
        if (!cfg.Habilitado)
            return;

        logger.LogInformation("Simulador ligado: cenário {Cenario}, ciclo a cada {Intervalo} s.", cfg.Cenario, cfg.IntervaloSegundos);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, cfg.IntervaloSegundos)));
        do
        {
            // Um ciclo que falha (ex.: banco reiniciando) não pode derrubar o simulador: registra e tenta no próximo.
            try
            {
                using var scope = scopes.CreateScope();
                var resultado = await scope.ServiceProvider.GetRequiredService<ISimuladorSensores>().ExecutarCicloAsync(stoppingToken);

                logger.LogInformation("Simulador, ciclo {Ciclo}: {Leituras} leituras, {Acoes} ações.",
                    resultado.Ciclo, resultado.Leituras.Count, resultado.AcoesExecutadas.Count);
                foreach (var acao in resultado.AcoesExecutadas)
                    logger.LogInformation("  {Acao}", acao);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha no ciclo do simulador.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

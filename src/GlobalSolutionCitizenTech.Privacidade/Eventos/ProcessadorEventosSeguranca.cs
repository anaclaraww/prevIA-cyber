using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Privacidade.Ia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GlobalSolutionCitizenTech.Privacidade.Eventos;

/// <summary>
/// Consome a fila: classifica o farol, grava o evento (auditoria) e pede à IA a mensagem para o cidadão.
/// O evento é gravado antes da tradução: o painel já mostra o bloqueio enquanto a IA escreve.
/// </summary>
public sealed class ProcessadorEventosSeguranca(
    FilaEventosSeguranca fila,
    IServiceScopeFactory escopos,
    ILogger<ProcessadorEventosSeguranca> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var pendente in fila.Leitor.ReadAllAsync(ct))
        {
            try
            {
                await ProcessarAsync(pendente, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Falha num evento não derruba a auditoria dos próximos.
                logger.LogError(ex, "Falha ao registrar evento de segurança {Tipo}", pendente.Tipo);
            }
        }
    }

    private async Task ProcessarAsync(EventoPendente p, CancellationToken ct)
    {
        using var escopo = escopos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<IAppDbContext>();
        var tradutor = escopo.ServiceProvider.GetRequiredService<ITradutorRisco>();

        var farol = RiscoCidadao.Classificar(p.Tipo, p.Categorias);
        var evento = new EventoSeguranca(p.Tipo, p.Sink, farol, p.Categorias, Limitar(p.DescricaoTecnica, 500),
            Limitar(p.Origem, 200), p.TraceId, p.OcorridoEm);
        db.EventosSeguranca.Add(evento);
        await db.SaveChangesAsync(ct);

        TraducaoRisco traducao;
        try
        {
            traducao = await tradutor.TraduzirAsync(evento, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Tradução via IA indisponível ({Erro}); usando o texto offline.", ex.GetType().Name);
            traducao = TradutorRiscoOffline.Traduzir(evento);
        }

        evento.RegistrarTraducao(Limitar(traducao.Mensagem, 600), traducao.TraduzidoPor);
        await db.SaveChangesAsync(ct);
    }

    private static string Limitar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..(tamanho - 1)] + "...";
}

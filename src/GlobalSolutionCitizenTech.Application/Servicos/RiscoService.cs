using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Domain.Servicos;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface IRiscoService
{
    Task<ResultadoAvaliacaoRiscoResponse> AvaliarAsync(Guid bairroId, int horas, CancellationToken ct);
    Task<IReadOnlyList<AvaliacaoRiscoResponse>> HistoricoAsync(Guid bairroId, CancellationToken ct);
}

/// <summary>
/// Avalia o risco de alagamento de um bairro (previsão Open-Meteo + entupimentos abertos) e age:
/// <list type="bullet">
/// <item>ALTO: quadras disponíveis entram em preparação, missões abertas viram prioridade alta, alerta ALTO.</item>
/// <item>MÉDIO: alerta MÉDIO.</item>
/// </list>
/// </summary>
public class RiscoService(IAppDbContext db, IPrevisaoTempoClient previsao) : IRiscoService
{
    public const int HorasPadrao = 6;

    public async Task<ResultadoAvaliacaoRiscoResponse> AvaliarAsync(Guid bairroId, int horas, CancellationToken ct)
    {
        var bairro = await db.Bairros.FirstOrDefaultAsync(b => b.Id == bairroId, ct)
                     ?? throw new RecursoNaoEncontradoException("Bairro", bairroId);

        var chuva = await previsao.ObterChuvaPrevistaMmAsync(bairro.Latitude, bairro.Longitude, horas, ct);
        var entupimentos = await db.Missoes.CountAsync(m => m.BairroId == bairroId
                                                            && m.Tipo == TipoMissao.Desobstrucao
                                                            && m.Status != StatusMissao.Concluida, ct);

        var nivel = CalculadoraRisco.Calcular(chuva, bairro.LimiarChuvaMm, entupimentos);
        var avaliacao = new AvaliacaoRisco(bairroId, chuva, horas, bairro.LimiarChuvaMm, entupimentos, nivel);
        db.AvaliacoesRisco.Add(avaliacao);

        var acoes = nivel switch
        {
            NivelRisco.Alto => await AgirRiscoAltoAsync(bairro, chuva, horas, ct),
            NivelRisco.Medio => await AgirRiscoMedioAsync(bairro, chuva, horas, ct),
            _ => ["Risco baixo: nenhuma ação necessária."]
        };

        await db.SaveChangesAsync(ct);
        return new ResultadoAvaliacaoRiscoResponse(avaliacao.ToResponse(), acoes);
    }

    public async Task<IReadOnlyList<AvaliacaoRiscoResponse>> HistoricoAsync(Guid bairroId, CancellationToken ct)
    {
        if (!await db.Bairros.AnyAsync(b => b.Id == bairroId, ct))
            throw new RecursoNaoEncontradoException("Bairro", bairroId);

        return (await db.AvaliacoesRisco.AsNoTracking()
                .Where(a => a.BairroId == bairroId)
                .OrderByDescending(a => a.CriadoEm)
                .Take(50)
                .ToListAsync(ct))
            .Select(a => a.ToResponse()).ToList();
    }

    private async Task<List<string>> AgirRiscoAltoAsync(Bairro bairro, decimal chuva, int horas, CancellationToken ct)
    {
        var acoes = new List<string>();

        var quadras = await db.Quadras
            .Where(q => q.BairroId == bairro.Id && q.Status == StatusQuadra.Disponivel)
            .ToListAsync(ct);
        foreach (var quadra in quadras)
        {
            quadra.AlterarStatus(StatusQuadra.EmPreparacao);
            acoes.Add($"{quadra.Nome} em preparação: acesso travado e comportas prontas.");
        }

        var missoesAbertas = await db.Missoes
            .Where(m => m.BairroId == bairro.Id && m.Status != StatusMissao.Concluida && m.Prioridade != PrioridadeMissao.Alta)
            .ToListAsync(ct);
        foreach (var missao in missoesAbertas)
            missao.ElevarPrioridade();
        if (missoesAbertas.Count > 0)
            acoes.Add($"{missoesAbertas.Count} missão(ões) de drenagem elevada(s) para prioridade alta.");

        acoes.AddRange(await CriarAlertaSeNecessarioAsync(bairro, NivelRisco.Alto,
            $"Risco ALTO de alagamento em {bairro.Nome}: {chuva} mm previstos nas próximas {horas} h. " +
            "Evite as áreas baixas; quadras do bairro estão recebendo água.", ct));

        return acoes;
    }

    private async Task<List<string>> AgirRiscoMedioAsync(Bairro bairro, decimal chuva, int horas, CancellationToken ct) =>
        await CriarAlertaSeNecessarioAsync(bairro, NivelRisco.Medio,
            $"Risco MÉDIO de alagamento em {bairro.Nome}: {chuva} mm previstos nas próximas {horas} h. Fique atento.", ct);

    /// <summary>Não duplica: só cria se não houver alerta do bairro ativo com nível igual ou maior.</summary>
    private async Task<List<string>> CriarAlertaSeNecessarioAsync(Bairro bairro, NivelRisco nivel, string mensagem, CancellationToken ct)
    {
        var existe = await db.Alertas.AnyAsync(a => a.BairroId == bairro.Id && a.QuadraId == null
                                                    && a.Ativo && a.Nivel >= nivel, ct);
        if (existe)
            return [$"Já existe alerta ativo de nível {nivel} ou superior para {bairro.Nome}."];

        db.Alertas.Add(new Alerta(bairro.Id, null, nivel, mensagem));
        return [$"Alerta {nivel} emitido para {bairro.Nome}."];
    }
}

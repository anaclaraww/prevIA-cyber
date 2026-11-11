using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Domain.Servicos;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

/// <summary>
/// Cria missões e atribui robôs. Não chama <c>SaveChanges</c>: quem orquestra (o serviço que
/// recebeu a requisição) salva tudo de uma vez, numa única transação.
/// </summary>
public interface IDespachoMissoes
{
    Task<Missao> CriarEDespacharAsync(TipoMissao tipo, Guid bairroId, Guid? sensorId,
        PrioridadeMissao prioridade, string descricao, CancellationToken ct);

    Task<bool> TentarAtribuirAsync(Missao missao, CancellationToken ct);

    /// <summary>Faz o robô assumir a missão pendente mais prioritária do bairro dele, se houver.</summary>
    Task<Missao?> AtribuirProximaPendenteAsync(Robo robo, CancellationToken ct);
}

public class DespachoMissoes(IAppDbContext db, TimeProvider relogio) : IDespachoMissoes
{
    public async Task<Missao> CriarEDespacharAsync(TipoMissao tipo, Guid bairroId, Guid? sensorId,
        PrioridadeMissao prioridade, string descricao, CancellationToken ct)
    {
        var missao = new Missao(tipo, bairroId, sensorId, prioridade, descricao);
        db.Missoes.Add(missao);
        await TentarAtribuirAsync(missao, ct);
        return missao;
    }

    public async Task<bool> TentarAtribuirAsync(Missao missao, CancellationToken ct)
    {
        var robosDoBairro = await db.Robos.Where(r => r.BairroBaseId == missao.BairroId).ToListAsync(ct);
        var robo = SeletorRobo.Escolher(robosDoBairro, missao.BairroId, missao.Tipo);
        if (robo is null)
            return false;

        missao.AtribuirRobo(robo, relogio.GetUtcNow());
        return true;
    }

    public async Task<Missao?> AtribuirProximaPendenteAsync(Robo robo, CancellationToken ct)
    {
        var pendentes = await db.Missoes
            .Where(m => m.BairroId == robo.BairroBaseId && m.Status == StatusMissao.Pendente)
            .ToListAsync(ct);

        var proxima = pendentes
            .Where(m => robo.PodeAssumir(m.Tipo))
            .OrderByDescending(m => m.Prioridade)
            .ThenBy(m => m.CriadoEm)
            .FirstOrDefault();

        proxima?.AtribuirRobo(robo, relogio.GetUtcNow());
        return proxima;
    }
}

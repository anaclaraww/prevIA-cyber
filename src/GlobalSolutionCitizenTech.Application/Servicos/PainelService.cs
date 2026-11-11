using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface IPainelService
{
    Task<PainelBairroResponse> ObterAsync(Guid bairroId, CancellationToken ct);
}

/// <summary>Visão consolidada de um bairro para a Defesa Civil: risco, quadras, robôs, missões e alertas.</summary>
public class PainelService(IAppDbContext db) : IPainelService
{
    public async Task<PainelBairroResponse> ObterAsync(Guid bairroId, CancellationToken ct)
    {
        var bairro = await db.Bairros.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bairroId, ct)
                     ?? throw new RecursoNaoEncontradoException("Bairro", bairroId);

        var ultimaAvaliacao = await db.AvaliacoesRisco.AsNoTracking()
            .Where(a => a.BairroId == bairroId).OrderByDescending(a => a.CriadoEm).FirstOrDefaultAsync(ct);
        var quadras = await db.Quadras.AsNoTracking().Where(q => q.BairroId == bairroId).OrderBy(q => q.Nome).ToListAsync(ct);
        var robos = await db.Robos.AsNoTracking().Where(r => r.BairroBaseId == bairroId).OrderBy(r => r.Codigo).ToListAsync(ct);
        var missoes = await db.Missoes.AsNoTracking().Include(m => m.Robo)
            .Where(m => m.BairroId == bairroId && m.Status != StatusMissao.Concluida).ToListAsync(ct);
        var alertas = await db.Alertas.AsNoTracking().Where(a => a.BairroId == bairroId && a.Ativo).ToListAsync(ct);

        return new PainelBairroResponse(
            bairro.ToResponse(),
            ultimaAvaliacao?.ToResponse(),
            quadras.Select(q => q.ToResponse()).ToList(),
            robos.Select(r => r.ToResponse()).ToList(),
            missoes.OrderByDescending(m => m.Prioridade).ThenBy(m => m.CriadoEm).Select(m => m.ToResponse()).ToList(),
            alertas.OrderByDescending(a => a.Nivel).ThenByDescending(a => a.CriadoEm).Select(a => a.ToResponse()).ToList(),
            quadras.Sum(q => q.VolumeRetidoM3));
    }
}

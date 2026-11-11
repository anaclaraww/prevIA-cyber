using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface IAlertaService
{
    Task<IReadOnlyList<AlertaResponse>> ListarAsync(Guid? bairroId, bool? ativo, CancellationToken ct);
    Task<AlertaResponse> EncerrarAsync(Guid id, CancellationToken ct);
}

public class AlertaService(IAppDbContext db, TimeProvider relogio) : IAlertaService
{
    public async Task<IReadOnlyList<AlertaResponse>> ListarAsync(Guid? bairroId, bool? ativo, CancellationToken ct)
    {
        var query = db.Alertas.AsNoTracking();
        if (bairroId is not null) query = query.Where(a => a.BairroId == bairroId);
        if (ativo is not null) query = query.Where(a => a.Ativo == ativo);
        return (await query.ToListAsync(ct))
            .OrderByDescending(a => a.Nivel).ThenByDescending(a => a.CriadoEm)
            .Select(a => a.ToResponse()).ToList();
    }

    public async Task<AlertaResponse> EncerrarAsync(Guid id, CancellationToken ct)
    {
        var alerta = await db.Alertas.FirstOrDefaultAsync(a => a.Id == id, ct)
                     ?? throw new RecursoNaoEncontradoException("Alerta", id);
        alerta.Encerrar(relogio.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return alerta.ToResponse();
    }
}

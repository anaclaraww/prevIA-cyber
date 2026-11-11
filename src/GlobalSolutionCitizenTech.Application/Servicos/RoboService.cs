using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface IRoboService
{
    Task<IReadOnlyList<RoboResponse>> ListarAsync(Guid? bairroId, StatusRobo? status, CancellationToken ct);
    Task<RoboResponse> ObterAsync(Guid id, CancellationToken ct);
    Task<RoboResponse> CriarAsync(CriarRoboRequest request, CancellationToken ct);
    Task<RoboResponse> RecarregarAsync(Guid id, CancellationToken ct);
    Task<RoboResponse> EnviarParaManutencaoAsync(Guid id, CancellationToken ct);
    Task RemoverAsync(Guid id, CancellationToken ct);
}

public class RoboService(IAppDbContext db, IDespachoMissoes despacho) : IRoboService
{
    public async Task<IReadOnlyList<RoboResponse>> ListarAsync(Guid? bairroId, StatusRobo? status, CancellationToken ct)
    {
        var query = db.Robos.AsNoTracking();
        if (bairroId is not null) query = query.Where(r => r.BairroBaseId == bairroId);
        if (status is not null) query = query.Where(r => r.Status == status);
        return (await query.OrderBy(r => r.Codigo).ToListAsync(ct)).Select(r => r.ToResponse()).ToList();
    }

    public async Task<RoboResponse> ObterAsync(Guid id, CancellationToken ct) =>
        (await BuscarAsync(id, ct)).ToResponse();

    /// <summary>Cadastra o robô e, se houver missão pendente no bairro, ele já a assume.</summary>
    public async Task<RoboResponse> CriarAsync(CriarRoboRequest r, CancellationToken ct)
    {
        var bairroId = r.BairroBaseId!.Value;
        if (!await db.Bairros.AnyAsync(b => b.Id == bairroId, ct))
            throw new RecursoNaoEncontradoException("Bairro", bairroId);

        var codigo = r.Codigo.Trim().ToUpperInvariant();
        if (await db.Robos.AnyAsync(x => x.Codigo == codigo, ct))
            throw new RegraNegocioException($"Já existe um robô com o código '{codigo}'.");

        var robo = new Robo(codigo, r.Tipo!.Value, bairroId, r.Bateria);
        db.Robos.Add(robo);
        await despacho.AtribuirProximaPendenteAsync(robo, ct);
        await db.SaveChangesAsync(ct);
        return robo.ToResponse();
    }

    public async Task<RoboResponse> RecarregarAsync(Guid id, CancellationToken ct)
    {
        var robo = await BuscarAsync(id, ct);
        robo.Recarregar();
        await despacho.AtribuirProximaPendenteAsync(robo, ct);
        await db.SaveChangesAsync(ct);
        return robo.ToResponse();
    }

    public async Task<RoboResponse> EnviarParaManutencaoAsync(Guid id, CancellationToken ct)
    {
        var robo = await BuscarAsync(id, ct);
        robo.EnviarParaManutencao();
        await db.SaveChangesAsync(ct);
        return robo.ToResponse();
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct)
    {
        var robo = await BuscarAsync(id, ct);
        if (robo.Status == StatusRobo.EmMissao)
            throw new RegraNegocioException($"O robô '{robo.Codigo}' está em missão e não pode ser removido.");
        if (await db.Missoes.AnyAsync(m => m.RoboId == id, ct))
            throw new RegraNegocioException($"O robô '{robo.Codigo}' possui histórico de missões; envie-o para manutenção em vez de remover.");

        db.Robos.Remove(robo);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Robo> BuscarAsync(Guid id, CancellationToken ct) =>
        await db.Robos.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new RecursoNaoEncontradoException("Robô", id);
}

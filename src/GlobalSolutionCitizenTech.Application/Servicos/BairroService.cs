using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface IBairroService
{
    Task<IReadOnlyList<BairroResponse>> ListarAsync(CancellationToken ct);
    Task<BairroResponse> ObterAsync(Guid id, CancellationToken ct);
    Task<BairroResponse> CriarAsync(BairroRequest request, CancellationToken ct);
    Task<BairroResponse> AtualizarAsync(Guid id, BairroRequest request, CancellationToken ct);
    Task RemoverAsync(Guid id, CancellationToken ct);
}

public class BairroService(IAppDbContext db) : IBairroService
{
    public async Task<IReadOnlyList<BairroResponse>> ListarAsync(CancellationToken ct) =>
        (await db.Bairros.AsNoTracking().OrderBy(b => b.Nome).ToListAsync(ct)).Select(b => b.ToResponse()).ToList();

    public async Task<BairroResponse> ObterAsync(Guid id, CancellationToken ct) =>
        (await BuscarAsync(id, ct)).ToResponse();

    public async Task<BairroResponse> CriarAsync(BairroRequest r, CancellationToken ct)
    {
        await GarantirNomeUnicoAsync(r.Nome, r.Cidade, null, ct);
        var bairro = new Bairro(r.Nome, r.Cidade, r.Uf, r.Latitude, r.Longitude, r.LimiarChuvaMm);
        db.Bairros.Add(bairro);
        await db.SaveChangesAsync(ct);
        return bairro.ToResponse();
    }

    public async Task<BairroResponse> AtualizarAsync(Guid id, BairroRequest r, CancellationToken ct)
    {
        var bairro = await BuscarAsync(id, ct);
        await GarantirNomeUnicoAsync(r.Nome, r.Cidade, id, ct);
        bairro.Atualizar(r.Nome, r.Cidade, r.Uf, r.Latitude, r.Longitude, r.LimiarChuvaMm);
        await db.SaveChangesAsync(ct);
        return bairro.ToResponse();
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct)
    {
        var bairro = await BuscarAsync(id, ct);
        var emUso = await db.Quadras.AnyAsync(q => q.BairroId == id, ct)
                    || await db.Sensores.AnyAsync(s => s.BairroId == id, ct)
                    || await db.Robos.AnyAsync(r => r.BairroBaseId == id, ct);
        if (emUso)
            throw new RegraNegocioException(
                $"O bairro '{bairro.Nome}' possui quadras, sensores ou robôs vinculados e não pode ser removido.");

        db.Bairros.Remove(bairro);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Bairro> BuscarAsync(Guid id, CancellationToken ct) =>
        await db.Bairros.FirstOrDefaultAsync(b => b.Id == id, ct)
        ?? throw new RecursoNaoEncontradoException("Bairro", id);

    private async Task GarantirNomeUnicoAsync(string nome, string cidade, Guid? ignorarId, CancellationToken ct)
    {
        var n = nome.Trim().ToLower();
        var c = cidade.Trim().ToLower();
        if (await db.Bairros.AnyAsync(b => b.Nome.ToLower() == n && b.Cidade.ToLower() == c && b.Id != ignorarId, ct))
            throw new RegraNegocioException($"Já existe o bairro '{nome}' em {cidade}.");
    }
}

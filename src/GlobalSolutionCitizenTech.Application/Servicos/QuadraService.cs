using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Servicos;

public interface IQuadraService
{
    Task<IReadOnlyList<QuadraResponse>> ListarAsync(Guid? bairroId, StatusQuadra? status, CancellationToken ct);
    Task<QuadraResponse> ObterAsync(Guid id, CancellationToken ct);
    Task<QuadraResponse> CriarAsync(CriarQuadraRequest request, CancellationToken ct);
    Task<QuadraResponse> AtualizarAsync(Guid id, AtualizarQuadraRequest request, CancellationToken ct);
    Task<QuadraResponse> AlterarStatusAsync(Guid id, StatusQuadra status, CancellationToken ct);
    Task RemoverAsync(Guid id, CancellationToken ct);
}

public class QuadraService(IAppDbContext db, ICepClient cepClient) : IQuadraService
{
    public async Task<IReadOnlyList<QuadraResponse>> ListarAsync(Guid? bairroId, StatusQuadra? status, CancellationToken ct)
    {
        var query = db.Quadras.AsNoTracking();
        if (bairroId is not null) query = query.Where(q => q.BairroId == bairroId);
        if (status is not null) query = query.Where(q => q.Status == status);
        return (await query.OrderBy(q => q.Nome).ToListAsync(ct)).Select(q => q.ToResponse()).ToList();
    }

    public async Task<QuadraResponse> ObterAsync(Guid id, CancellationToken ct) =>
        (await BuscarAsync(id, ct)).ToResponse();

    /// <summary>Cria a quadra preenchendo o logradouro pelo CEP (ViaCEP).</summary>
    public async Task<QuadraResponse> CriarAsync(CriarQuadraRequest r, CancellationToken ct)
    {
        var bairroId = r.BairroId!.Value;
        if (!await db.Bairros.AnyAsync(b => b.Id == bairroId, ct))
            throw new RecursoNaoEncontradoException("Bairro", bairroId);

        var cep = new string(r.Cep.Where(char.IsDigit).ToArray());
        var endereco = await cepClient.ConsultarAsync(cep, ct)
                       ?? throw new DadosInvalidosException($"CEP '{r.Cep}' não encontrado.");

        var quadra = new Quadra(r.Nome, bairroId, cep, endereco.Logradouro, r.Numero, r.Latitude, r.Longitude, r.CapacidadeM3);
        db.Quadras.Add(quadra);
        await db.SaveChangesAsync(ct);
        return quadra.ToResponse();
    }

    public async Task<QuadraResponse> AtualizarAsync(Guid id, AtualizarQuadraRequest r, CancellationToken ct)
    {
        var quadra = await BuscarAsync(id, ct);
        quadra.Atualizar(r.Nome, r.Numero, r.CapacidadeM3);
        await db.SaveChangesAsync(ct);
        return quadra.ToResponse();
    }

    public async Task<QuadraResponse> AlterarStatusAsync(Guid id, StatusQuadra status, CancellationToken ct)
    {
        var quadra = await BuscarAsync(id, ct);
        quadra.AlterarStatus(status);
        await db.SaveChangesAsync(ct);
        return quadra.ToResponse();
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct)
    {
        var quadra = await BuscarAsync(id, ct);
        if (quadra.Status is StatusQuadra.EmPreparacao or StatusQuadra.Retendo or StatusQuadra.Esvaziando)
            throw new RegraNegocioException($"A quadra '{quadra.Nome}' está em operação ({quadra.Status}) e não pode ser removida.");
        if (await db.Sensores.AnyAsync(s => s.QuadraId == id, ct))
            throw new RegraNegocioException($"A quadra '{quadra.Nome}' possui sensores vinculados e não pode ser removida.");

        db.Quadras.Remove(quadra);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Quadra> BuscarAsync(Guid id, CancellationToken ct) =>
        await db.Quadras.FirstOrDefaultAsync(q => q.Id == id, ct)
        ?? throw new RecursoNaoEncontradoException("Quadra", id);
}

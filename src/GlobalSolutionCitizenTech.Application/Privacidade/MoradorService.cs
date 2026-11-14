using System.ComponentModel.DataAnnotations;
using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Privacidade;

public record CriarMoradorRequest(
    [Required, StringLength(120, MinimumLength = 3)] string Nome,
    [Required] Guid? BairroId,
    [Required] string Cpf,
    [StringLength(20)] string? Rg,
    [Required, RegularExpression(@"^\+?[\d\s().-]{10,20}$", ErrorMessage = "Telefone inválido.")] string Telefone,
    [StringLength(500)] string? CondicaoSaude,
    PrioridadeEvacuacao? Prioridade);

/// <summary>Visão do morador para listagens: só dados mascarados, nunca a condição de saúde.</summary>
public record MoradorResponse(Guid Id, string Nome, Guid BairroId, string CpfMascarado, string? RgMascarado,
    string TelefoneMascarado, bool PossuiCondicaoSaude, PrioridadeEvacuacao Prioridade, DateTimeOffset CriadoEm);

/// <summary>Contato para a equipe de evacuação: telefone e condição de saúde decifrados.</summary>
public record ContatoEmergenciaResponse(Guid Id, string Nome, Guid BairroId, string Telefone, string? CondicaoSaude,
    PrioridadeEvacuacao Prioridade);

public interface IMoradorService
{
    Task<MoradorResponse> CriarAsync(CriarMoradorRequest request, CancellationToken ct);
    Task<IReadOnlyList<MoradorResponse>> ListarAsync(Guid? bairroId, CancellationToken ct);
    Task<ContatoEmergenciaResponse> ContatoEmergenciaAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Cadastro de moradores vulneráveis. O CPF vira máscara + hash, o RG só máscara, o telefone e a condição de
/// saúde são cifrados: o banco nunca recebe esses dados em texto claro.
/// </summary>
public class MoradorService(IAppDbContext db, IProtetorDados protetor) : IMoradorService
{
    public async Task<MoradorResponse> CriarAsync(CriarMoradorRequest r, CancellationToken ct)
    {
        // As mensagens de erro nunca repetem o valor recebido: erro também é uma saída.
        if (!Cpf.Valido(r.Cpf))
            throw new DadosInvalidosException("CPF inválido.");

        if (!await db.Bairros.AnyAsync(b => b.Id == r.BairroId, ct))
            throw new RecursoNaoEncontradoException("Bairro", r.BairroId!);

        var cpfHash = protetor.Hash(Mascaras.SoDigitos(r.Cpf));
        if (await db.Moradores.AnyAsync(m => m.CpfHash == cpfHash, ct))
            throw new RegraNegocioException("Este morador já está cadastrado.");

        var condicao = string.IsNullOrWhiteSpace(r.CondicaoSaude) ? null : protetor.Cifrar(r.CondicaoSaude.Trim());
        var morador = new Morador(
            r.Nome, r.BairroId!.Value,
            Mascaras.Cpf(r.Cpf), cpfHash,
            string.IsNullOrWhiteSpace(r.Rg) ? null : Mascaras.Parcial(r.Rg),
            Mascaras.Telefone(r.Telefone), protetor.Cifrar(Mascaras.SoDigitos(r.Telefone)),
            condicao, r.Prioridade ?? PrioridadeEvacuacao.Normal);

        db.Moradores.Add(morador);
        await db.SaveChangesAsync(ct);
        return morador.ToResponse();
    }

    public async Task<IReadOnlyList<MoradorResponse>> ListarAsync(Guid? bairroId, CancellationToken ct)
    {
        var consulta = db.Moradores.AsNoTracking();
        if (bairroId is not null)
            consulta = consulta.Where(m => m.BairroId == bairroId);

        return (await consulta.OrderByDescending(m => m.Prioridade).ThenBy(m => m.Nome).ToListAsync(ct))
            .Select(m => m.ToResponse()).ToList();
    }

    public async Task<ContatoEmergenciaResponse> ContatoEmergenciaAsync(Guid id, CancellationToken ct)
    {
        var m = await db.Moradores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new RecursoNaoEncontradoException("Morador", id);

        return new ContatoEmergenciaResponse(m.Id, m.Nome, m.BairroId,
            FormatarTelefone(protetor.Decifrar(m.TelefoneCifrado)),
            m.CondicaoSaudeCifrada is null ? null : protetor.Decifrar(m.CondicaoSaudeCifrada),
            m.Prioridade);
    }

    private static string FormatarTelefone(string d) =>
        d.Length == 11 ? $"({d[..2]}) {d[2..7]}-{d[7..]}" : d.Length == 10 ? $"({d[..2]}) {d[2..6]}-{d[6..]}" : d;
}

public static class MapeamentosPrivacidade
{
    public static MoradorResponse ToResponse(this Morador m) =>
        new(m.Id, m.Nome, m.BairroId, m.CpfMascarado, m.RgMascarado, m.TelefoneMascarado, m.PossuiCondicaoSaude,
            m.Prioridade, m.CriadoEm);

    public static EventoSegurancaResponse ToResponse(this EventoSeguranca e) =>
        new(e.Id, e.CriadoEm, e.Tipo, e.Sink, e.Farol, e.ListaCategorias(), e.DescricaoTecnica, e.Origem, e.TraceId,
            e.MensagemCidadao, e.TraduzidoPor);
}

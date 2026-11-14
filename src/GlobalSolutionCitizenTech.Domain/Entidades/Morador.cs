using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>
/// Morador vulnerável de área de risco, cadastrado para a Defesa Civil priorizar a evacuação.
/// </summary>
/// <remarks>
/// A entidade nunca guarda CPF, RG, telefone ou condição de saúde em texto claro: recebe os valores já
/// protegidos pela camada de privacidade (máscara para exibição, hash para busca, cifra para o que a
/// Defesa Civil precisa ler numa emergência).
/// </remarks>
public class Morador : Entidade
{
    public string Nome { get; private set; } = null!;
    public Guid BairroId { get; private set; }

    /// <summary>CPF mascarado para exibição (ex.: <c>***.456.***-00</c>).</summary>
    public string CpfMascarado { get; private set; } = null!;

    /// <summary>HMAC-SHA256 do CPF: permite achar duplicidade sem guardar o número.</summary>
    public string CpfHash { get; private set; } = null!;

    /// <summary>RG mascarado. O número completo não é guardado (minimização, LGPD art. 6º, III).</summary>
    public string? RgMascarado { get; private set; }

    public string TelefoneMascarado { get; private set; } = null!;

    /// <summary>Telefone cifrado (AES-GCM): a Defesa Civil precisa ligar numa evacuação.</summary>
    public string TelefoneCifrado { get; private set; } = null!;

    /// <summary>Condição de saúde cifrada (AES-GCM). Dado sensível: nunca sai em log nem em listagem.</summary>
    public string? CondicaoSaudeCifrada { get; private set; }

    public PrioridadeEvacuacao Prioridade { get; private set; }

    public bool PossuiCondicaoSaude => CondicaoSaudeCifrada is not null;

    private Morador() { }

    public Morador(string nome, Guid bairroId, string cpfMascarado, string cpfHash, string? rgMascarado,
        string telefoneMascarado, string telefoneCifrado, string? condicaoSaudeCifrada, PrioridadeEvacuacao prioridade)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new RegraNegocioException("O nome do morador é obrigatório.");
        if (string.IsNullOrWhiteSpace(cpfHash) || string.IsNullOrWhiteSpace(telefoneCifrado))
            throw new RegraNegocioException("Os dados do morador precisam chegar protegidos.");

        Nome = nome.Trim();
        BairroId = bairroId;
        CpfMascarado = cpfMascarado;
        CpfHash = cpfHash;
        RgMascarado = rgMascarado;
        TelefoneMascarado = telefoneMascarado;
        TelefoneCifrado = telefoneCifrado;
        CondicaoSaudeCifrada = condicaoSaudeCifrada;

        // Quem depende de equipamento ou não se locomove sai primeiro: a condição de saúde eleva a prioridade.
        Prioridade = condicaoSaudeCifrada is not null && prioridade == PrioridadeEvacuacao.Normal
            ? PrioridadeEvacuacao.Alta
            : prioridade;
    }
}

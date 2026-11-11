using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>
/// Quadra pública que vira reservatório temporário em chuva forte.
/// O status segue uma máquina de estados: só transições previstas em <see cref="Transicoes"/> são aceitas.
/// </summary>
public class Quadra : Entidade
{
    public const decimal PercentualQuadraCheia = 90m;

    private static readonly Dictionary<StatusQuadra, StatusQuadra[]> Transicoes = new()
    {
        [StatusQuadra.Disponivel] = [StatusQuadra.EmPreparacao, StatusQuadra.Manutencao],
        [StatusQuadra.EmPreparacao] = [StatusQuadra.Retendo, StatusQuadra.Disponivel],
        [StatusQuadra.Retendo] = [StatusQuadra.Esvaziando],
        [StatusQuadra.Esvaziando] = [StatusQuadra.Disponivel],
        [StatusQuadra.Manutencao] = [StatusQuadra.Disponivel],
    };

    public string Nome { get; private set; } = null!;
    public Guid BairroId { get; private set; }
    public Bairro? Bairro { get; private set; }
    public string Cep { get; private set; } = null!;
    public string Logradouro { get; private set; } = null!;
    public string? Numero { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public decimal CapacidadeM3 { get; private set; }
    public decimal NivelAtualPercentual { get; private set; }
    public StatusQuadra Status { get; private set; } = StatusQuadra.Disponivel;

    public decimal VolumeRetidoM3 => Math.Round(CapacidadeM3 * NivelAtualPercentual / 100m, 2);
    public bool ProximaDaCapacidade => NivelAtualPercentual >= PercentualQuadraCheia;

    private Quadra() { }

    public Quadra(string nome, Guid bairroId, string cep, string logradouro, string? numero,
        double latitude, double longitude, decimal capacidadeM3)
    {
        Nome = nome.Trim();
        BairroId = bairroId;
        Cep = cep;
        Logradouro = logradouro;
        Numero = numero;
        Latitude = latitude;
        Longitude = longitude;
        DefinirCapacidade(capacidadeM3);
    }

    public void Atualizar(string nome, string? numero, decimal capacidadeM3)
    {
        Nome = nome.Trim();
        Numero = numero;
        DefinirCapacidade(capacidadeM3);
    }

    public bool PodeTransicionarPara(StatusQuadra novoStatus) =>
        Transicoes.TryGetValue(Status, out var permitidos) && permitidos.Contains(novoStatus);

    public void AlterarStatus(StatusQuadra novoStatus)
    {
        if (!PodeTransicionarPara(novoStatus))
            throw new RegraNegocioException(
                $"Transição de status inválida para a quadra '{Nome}': {Status} → {novoStatus}.");

        Status = novoStatus;
        if (novoStatus == StatusQuadra.Disponivel)
            NivelAtualPercentual = 0;
    }

    /// <summary>
    /// Registra o nível medido pelo sensor. Se a quadra estava em preparação e começou a
    /// receber água, passa automaticamente para <see cref="StatusQuadra.Retendo"/>.
    /// </summary>
    public void AtualizarNivel(decimal percentual)
    {
        if (percentual is < 0 or > 100)
            throw new RegraNegocioException("O nível da quadra deve estar entre 0 e 100%.");

        NivelAtualPercentual = percentual;
        if (Status == StatusQuadra.EmPreparacao && percentual > 0)
            Status = StatusQuadra.Retendo;
    }

    private void DefinirCapacidade(decimal capacidadeM3)
    {
        if (capacidadeM3 <= 0)
            throw new RegraNegocioException("A capacidade da quadra deve ser maior que zero.");
        CapacidadeM3 = capacidadeM3;
    }
}

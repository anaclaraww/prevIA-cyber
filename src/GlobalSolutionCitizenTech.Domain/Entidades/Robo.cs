using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>Robô de drenagem (inspetor ou desentupidor) com base em um bairro.</summary>
public class Robo : Entidade
{
    public const int BateriaMinimaParaMissao = 30;
    public const int ConsumoBateriaPorMissao = 15;

    public string Codigo { get; private set; } = null!;
    public TipoRobo Tipo { get; private set; }
    public Guid BairroBaseId { get; private set; }
    public Bairro? BairroBase { get; private set; }
    public int Bateria { get; private set; }
    public StatusRobo Status { get; private set; } = StatusRobo.Disponivel;

    private Robo() { }

    public Robo(string codigo, TipoRobo tipo, Guid bairroBaseId, int bateria)
    {
        if (bateria is < 0 or > 100)
            throw new RegraNegocioException("A bateria do robô deve estar entre 0 e 100%.");

        Codigo = codigo.Trim().ToUpperInvariant();
        Tipo = tipo;
        BairroBaseId = bairroBaseId;
        Bateria = bateria;
    }

    public static TipoRobo TipoNecessario(TipoMissao missao) => missao switch
    {
        TipoMissao.Desobstrucao => TipoRobo.Desentupidor,
        TipoMissao.Inspecao => TipoRobo.Inspetor,
        _ => throw new ArgumentOutOfRangeException(nameof(missao))
    };

    public bool PodeAssumir(TipoMissao missao) =>
        Status == StatusRobo.Disponivel
        && Bateria >= BateriaMinimaParaMissao
        && Tipo == TipoNecessario(missao);

    internal void IniciarMissao(TipoMissao missao)
    {
        if (!PodeAssumir(missao))
            throw new RegraNegocioException(
                $"O robô '{Codigo}' não pode assumir a missão (status {Status}, bateria {Bateria}%, tipo {Tipo}).");
        Status = StatusRobo.EmMissao;
    }

    internal void FinalizarMissao()
    {
        Bateria = Math.Max(0, Bateria - ConsumoBateriaPorMissao);
        Status = StatusRobo.Disponivel;
    }

    public void Recarregar()
    {
        if (Status == StatusRobo.EmMissao)
            throw new RegraNegocioException($"O robô '{Codigo}' está em missão e não pode ser recarregado agora.");
        Bateria = 100;
        Status = StatusRobo.Disponivel;
    }

    public void EnviarParaManutencao()
    {
        if (Status == StatusRobo.EmMissao)
            throw new RegraNegocioException($"O robô '{Codigo}' está em missão e não pode ir para manutenção agora.");
        Status = StatusRobo.Manutencao;
    }
}

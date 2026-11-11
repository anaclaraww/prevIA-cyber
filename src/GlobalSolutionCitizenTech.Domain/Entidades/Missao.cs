using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>Ordem de serviço para um robô (ex.: desobstruir a boca de lobo de um sensor).</summary>
public class Missao : Entidade
{
    public TipoMissao Tipo { get; private set; }
    public Guid BairroId { get; private set; }
    public Guid? SensorId { get; private set; }
    public Sensor? Sensor { get; private set; }
    public Guid? RoboId { get; private set; }
    public Robo? Robo { get; private set; }
    public PrioridadeMissao Prioridade { get; private set; }
    public StatusMissao Status { get; private set; } = StatusMissao.Pendente;
    public string Descricao { get; private set; } = null!;
    public DateTimeOffset? IniciadaEm { get; private set; }
    public DateTimeOffset? ConcluidaEm { get; private set; }

    public bool Aberta => Status != StatusMissao.Concluida;

    private Missao() { }

    public Missao(TipoMissao tipo, Guid bairroId, Guid? sensorId, PrioridadeMissao prioridade, string descricao)
    {
        Tipo = tipo;
        BairroId = bairroId;
        SensorId = sensorId;
        Prioridade = prioridade;
        Descricao = descricao;
    }

    public void AtribuirRobo(Robo robo, DateTimeOffset momento)
    {
        if (Status != StatusMissao.Pendente)
            throw new RegraNegocioException("Só é possível atribuir robô a uma missão pendente.");

        robo.IniciarMissao(Tipo);
        Robo = robo;
        RoboId = robo.Id;
        Status = StatusMissao.EmAndamento;
        IniciadaEm = momento;
    }

    public void Concluir(DateTimeOffset momento)
    {
        if (Status != StatusMissao.EmAndamento || Robo is null)
            throw new RegraNegocioException("Só é possível concluir uma missão em andamento.");

        Robo.FinalizarMissao();
        Status = StatusMissao.Concluida;
        ConcluidaEm = momento;
    }

    public void ElevarPrioridade()
    {
        if (Aberta)
            Prioridade = PrioridadeMissao.Alta;
    }
}

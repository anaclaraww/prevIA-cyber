using GlobalSolutionCitizenTech.Domain.Comum;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>Medição enviada por um sensor. Append-only.</summary>
public class Leitura : Entidade
{
    public Guid SensorId { get; private set; }
    public decimal Valor { get; private set; }
    public DateTimeOffset RegistradaEm { get; private set; }

    private Leitura() { }

    internal Leitura(Guid sensorId, decimal valor, DateTimeOffset registradaEm)
    {
        SensorId = sensorId;
        Valor = valor;
        RegistradaEm = registradaEm;
    }
}

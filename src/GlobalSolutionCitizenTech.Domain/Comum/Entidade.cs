namespace GlobalSolutionCitizenTech.Domain.Comum;

/// <summary>Base de todas as entidades: identificador e data de criação.</summary>
public abstract class Entidade
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CriadoEm { get; protected set; } = DateTimeOffset.UtcNow;
}

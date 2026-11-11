using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>Alerta para a Defesa Civil e a população de um bairro.</summary>
public class Alerta : Entidade
{
    public Guid BairroId { get; private set; }
    public Guid? QuadraId { get; private set; }
    public NivelRisco Nivel { get; private set; }
    public string Mensagem { get; private set; } = null!;
    public bool Ativo { get; private set; } = true;
    public DateTimeOffset? EncerradoEm { get; private set; }

    private Alerta() { }

    public Alerta(Guid bairroId, Guid? quadraId, NivelRisco nivel, string mensagem)
    {
        BairroId = bairroId;
        QuadraId = quadraId;
        Nivel = nivel;
        Mensagem = mensagem;
    }

    public void Encerrar(DateTimeOffset momento)
    {
        if (!Ativo)
            throw new RegraNegocioException("O alerta já está encerrado.");
        Ativo = false;
        EncerradoEm = momento;
    }
}

using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Servicos;

/// <summary>
/// Escolhe o robô para uma missão: do mesmo bairro, do tipo certo, disponível e com bateria mínima.
/// Entre os aptos, vence o de maior bateria.
/// </summary>
public static class SeletorRobo
{
    public static Robo? Escolher(IEnumerable<Robo> candidatos, Guid bairroId, TipoMissao tipoMissao) =>
        candidatos
            .Where(r => r.BairroBaseId == bairroId && r.PodeAssumir(tipoMissao))
            .OrderByDescending(r => r.Bateria)
            .ThenBy(r => r.Codigo)
            .FirstOrDefault();
}

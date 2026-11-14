namespace GlobalSolutionCitizenTech.Privacidade.Taint;

/// <summary>
/// Declara que o endpoint devolve dado sensível de propósito (ex.: o telefone para a equipe de resgate, o
/// token JWT do login). A resposta não é mascarada; quem pode acessar continua sendo decidido pelo
/// <c>[Authorize]</c> do endpoint (o login é o único anônimo com o atributo).
/// </summary>
/// <remarks>
/// É uma exceção explícita, revisável por busca no código: todo endpoint sem ela passa pela barreira.
/// Os logs continuam protegidos, com ou sem o atributo.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PermiteDadosSensiveisAttribute(string justificativa) : Attribute
{
    public string Justificativa { get; } = justificativa;
}

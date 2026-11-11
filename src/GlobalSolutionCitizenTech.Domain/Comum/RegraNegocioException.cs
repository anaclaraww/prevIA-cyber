namespace GlobalSolutionCitizenTech.Domain.Comum;

/// <summary>
/// Violação de uma regra de negócio do domínio (ex.: transição de estado inválida).
/// A API traduz para HTTP 409 Conflict.
/// </summary>
public class RegraNegocioException(string mensagem) : Exception(mensagem);

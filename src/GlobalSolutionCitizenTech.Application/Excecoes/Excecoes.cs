namespace GlobalSolutionCitizenTech.Application.Excecoes;

/// <summary>Recurso inexistente → HTTP 404.</summary>
public class RecursoNaoEncontradoException(string recurso, object id)
    : Exception($"{recurso} '{id}' não encontrado(a).");

/// <summary>Dados de entrada inválidos que só podem ser verificados no serviço (ex.: CEP inexistente) → HTTP 400.</summary>
public class DadosInvalidosException(string mensagem) : Exception(mensagem);

/// <summary>Falha ao chamar uma API externa (Open-Meteo, ViaCEP) → HTTP 502.</summary>
public class ServicoExternoException(string servico, string mensagem, Exception? inner = null)
    : Exception($"Falha ao consultar {servico}: {mensagem}", inner)
{
    public string Servico { get; } = servico;
}

namespace GlobalSolutionCitizenTech.Domain.Enums;

/// <summary>Categoria do dado pessoal (PII) encontrado pelo motor de parsing.</summary>
public enum CategoriaPii
{
    /// <summary>Documento de identificação: CPF, RG, cartão SUS.</summary>
    Documento,

    /// <summary>Dado de saúde: laudo, condição, CID, medicamento (dado sensível na LGPD, art. 5º, II).</summary>
    Saude,

    /// <summary>Credencial: senha, segredo de cliente, token.</summary>
    Credencial,

    /// <summary>Contato: telefone, e-mail.</summary>
    Contato
}

/// <summary>O que aconteceu com o dado pessoal.</summary>
public enum TipoEventoSeguranca
{
    /// <summary>PII recebida (source) e protegida: tagueada, mascarada e/ou cifrada antes de gravar.</summary>
    PiiProtegida,

    /// <summary>Um log tentou imprimir PII; o valor foi mascarado ou a linha bloqueada.</summary>
    VazamentoBloqueadoLog,

    /// <summary>Uma resposta HTTP carregava PII; os valores foram mascarados.</summary>
    VazamentoMascaradoResposta,

    /// <summary>Uma resposta HTTP não autenticada carregava PII; a resposta foi bloqueada.</summary>
    VazamentoBloqueadoResposta
}

/// <summary>Saída insegura (sink) onde o taint analysis atuou.</summary>
public enum SinkSeguranca
{
    /// <summary>Entrada da API (source), não é saída.</summary>
    Entrada,
    Log,
    RespostaHttp
}

/// <summary>Farol de risco ao cidadão.</summary>
public enum Farol
{
    Verde,
    Amarelo,
    Vermelho
}

/// <summary>Prioridade de evacuação de um morador vulnerável.</summary>
public enum PrioridadeEvacuacao
{
    Normal,
    Alta,
    Critica
}

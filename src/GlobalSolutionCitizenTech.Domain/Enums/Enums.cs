namespace GlobalSolutionCitizenTech.Domain.Enums;

/// <summary>Ciclo de vida da quadra inteligente.</summary>
public enum StatusQuadra
{
    /// <summary>Uso esportivo normal.</summary>
    Disponivel = 1,
    /// <summary>Risco alto: acesso travado e comportas prontas.</summary>
    EmPreparacao = 2,
    /// <summary>Recebendo e retendo água da chuva.</summary>
    Retendo = 3,
    /// <summary>Liberando a água devagar depois do pico.</summary>
    Esvaziando = 4,
    /// <summary>Fora de operação.</summary>
    Manutencao = 5
}

public enum TipoSensor
{
    /// <summary>Mede a obstrução da boca de lobo, em % (0 a 100).</summary>
    BocaDeLobo = 1,
    /// <summary>Mede a chuva, em mm/h.</summary>
    Pluviometro = 2,
    /// <summary>Mede o nível de água da quadra, em % da capacidade.</summary>
    NivelQuadra = 3
}

public enum TipoRobo
{
    /// <summary>Percorre galerias e mapeia entupimentos.</summary>
    Inspetor = 1,
    /// <summary>Desobstrui bocas de lobo e galerias.</summary>
    Desentupidor = 2
}

public enum StatusRobo
{
    Disponivel = 1,
    EmMissao = 2,
    Manutencao = 3
}

public enum TipoMissao
{
    Desobstrucao = 1,
    Inspecao = 2
}

public enum StatusMissao
{
    Pendente = 1,
    EmAndamento = 2,
    Concluida = 3
}

public enum PrioridadeMissao
{
    Normal = 1,
    Alta = 2
}

public enum NivelRisco
{
    Baixo = 1,
    Medio = 2,
    Alto = 3
}

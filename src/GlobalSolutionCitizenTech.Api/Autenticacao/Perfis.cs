namespace GlobalSolutionCitizenTech.Api.Autenticacao;

/// <summary>Perfis de acesso, gravados na claim <c>role</c> do JWT.</summary>
public static class Perfis
{
    /// <summary>Gateway dos sensores IoT: só envia leituras.</summary>
    public const string Sensor = "Sensor";

    /// <summary>Defesa Civil / operação: consulta tudo e opera (risco, quadras, missões, alertas).</summary>
    public const string Operador = "Operador";

    /// <summary>Administração: tudo, inclusive cadastros e o simulador.</summary>
    public const string Admin = "Admin";

    public static readonly string[] Todos = [Sensor, Operador, Admin];
}

/// <summary>Combinações de perfis usadas em <c>[Authorize(Roles = ...)]</c>.</summary>
public static class Acesso
{
    /// <summary>Consultas (GET).</summary>
    public const string Leitura = Perfis.Operador + "," + Perfis.Admin;

    /// <summary>Ações operacionais: avaliar risco, mudar status da quadra, missões, alertas, robôs.</summary>
    public const string Operacao = Perfis.Operador + "," + Perfis.Admin;

    /// <summary>Cadastros (criar, alterar e remover bairros, quadras, sensores e robôs) e simulador.</summary>
    public const string Cadastro = Perfis.Admin;

    /// <summary>Envio de leituras de sensores.</summary>
    public const string EnvioLeitura = Perfis.Sensor + "," + Perfis.Admin;
}

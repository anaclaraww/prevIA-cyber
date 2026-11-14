using System.Text.RegularExpressions;

namespace GlobalSolutionCitizenTech.Application.Privacidade;

/// <summary>
/// Máscaras de exibição de dados pessoais. Funções puras, usadas pelo motor de parsing (mascarar o que
/// detectou) e pelo cadastro de moradores (guardar só a versão mascarada).
/// </summary>
public static partial class Mascaras
{
    /// <summary><c>123.456.789-09</c> -> <c>***.456.***-09</c> (o formato pedido no enunciado).</summary>
    public static string Cpf(string cpf)
    {
        var d = SoDigitos(cpf);
        return d.Length == 11 ? $"***.{d[3..6]}.***-{d[9..]}" : Total(cpf);
    }

    /// <summary>Mantém só os 2 últimos caracteres: <c>12.345.678-9</c> -> <c>**********-9</c>.</summary>
    public static string Parcial(string valor)
    {
        var limpo = valor.Trim();
        return limpo.Length <= 2 ? new string('*', limpo.Length) : new string('*', limpo.Length - 2) + limpo[^2..];
    }

    /// <summary><c>(11) 98765-4321</c> -> <c>(11) *****-4321</c>.</summary>
    public static string Telefone(string telefone)
    {
        var d = SoDigitos(telefone);
        if (d.StartsWith("55") && d.Length > 11) d = d[2..];
        return d.Length switch
        {
            11 => $"({d[..2]}) *****-{d[7..]}",
            10 => $"({d[..2]}) ****-{d[6..]}",
            _ => Parcial(telefone)
        };
    }

    /// <summary><c>maria.silva@email.com</c> -> <c>m***@email.com</c>.</summary>
    public static string Email(string email)
    {
        var arroba = email.IndexOf('@');
        return arroba <= 0 ? Parcial(email) : $"{email[0]}***{email[arroba..]}";
    }

    /// <summary>Esconde tudo, indicando só o tipo do dado.</summary>
    public static string Total(string _, string rotulo = "protegido") => $"[{rotulo}]";

    public static string SoDigitos(string valor) => NaoDigito().Replace(valor, "");

    [GeneratedRegex(@"\D")]
    private static partial Regex NaoDigito();
}

/// <summary>Validação de CPF pelos dígitos verificadores (evita tratar qualquer número de 11 dígitos como CPF).</summary>
public static class Cpf
{
    public static bool Valido(string cpf)
    {
        var d = Mascaras.SoDigitos(cpf);
        if (d.Length != 11 || d.Distinct().Count() == 1)
            return false;

        return d[9] - '0' == Digito(d, 9) && d[10] - '0' == Digito(d, 10);

        static int Digito(string d, int tamanho)
        {
            var soma = 0;
            for (var i = 0; i < tamanho; i++)
                soma += (d[i] - '0') * (tamanho + 1 - i);
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }
    }
}

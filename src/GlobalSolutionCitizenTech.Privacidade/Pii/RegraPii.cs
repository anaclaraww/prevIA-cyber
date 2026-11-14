using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Privacidade.Pii;

public enum TipoMascara
{
    /// <summary><c>***.456.***-09</c></summary>
    Cpf,
    /// <summary><c>(11) *****-4321</c></summary>
    Telefone,
    /// <summary><c>m***@email.com</c></summary>
    Email,
    /// <summary>Só os 2 últimos caracteres visíveis.</summary>
    Parcial,
    /// <summary>Valor inteiro trocado por <c>[rótulo]</c>.</summary>
    Total
}

/// <summary>
/// Uma regra do motor de parsing. Combina o <b>nome do campo</b> (ex.: <c>cpf</c>, <c>laudo</c>) e/ou o
/// <b>formato do valor</b> (regex), com um validador opcional para cortar falso positivo.
/// </summary>
/// <remarks>
/// Novas regras entram por configuração, sem recompilar: seção <c>Privacidade:RegrasAdicionais</c>.
/// O nome do campo é comparado já normalizado (minúsculo, só letras e dígitos: <c>client_Secret</c> -> <c>clientsecret</c>).
/// </remarks>
public sealed class RegraPii
{
    /// <summary>Nome curto e único (aparece na auditoria): <c>CPF</c>, <c>Saude</c>...</summary>
    public string Nome { get; set; } = "";

    public CategoriaPii Categoria { get; set; }

    /// <summary>Regex aplicada ao nome do campo normalizado.</summary>
    public string? PadraoChave { get; set; }

    /// <summary>Regex aplicada ao valor (acha o dado mesmo em texto livre ou num campo de nome inocente).</summary>
    public string? PadraoValor { get; set; }

    /// <summary>Só aceita o valor se o nome do campo também casar (para formatos muito genéricos).</summary>
    public bool ValorExigeChave { get; set; }

    /// <summary>Validador extra do valor encontrado. Hoje: <c>cpf</c> (dígitos verificadores).</summary>
    public string? Validador { get; set; }

    public TipoMascara Mascara { get; set; } = TipoMascara.Total;

    /// <summary>Rótulo da máscara total (ex.: <c>[dado de saúde protegido]</c>).</summary>
    public string Rotulo { get; set; } = "dado pessoal protegido";

    /// <summary>
    /// Quando o valor casa no meio de um texto, esconde o texto inteiro e não só o trecho
    /// (ex.: "dependente de oxigênio": esconder só "oxigênio" ainda revelaria a condição).
    /// </summary>
    public bool MascararValorInteiro { get; set; }
}

/// <summary>Regras que já vêm no motor. A configuração pode desativar (por nome) e acrescentar regras.</summary>
public static class RegrasPadrao
{
    public static IReadOnlyList<RegraPii> Todas { get; } =
    [
        new()
        {
            Nome = "CPF", Categoria = CategoriaPii.Documento,
            PadraoChave = "cpf|documento",
            PadraoValor = @"(?<![\w.])\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?![\w])",
            Validador = "cpf", Mascara = TipoMascara.Cpf
        },
        new()
        {
            Nome = "RG", Categoria = CategoriaPii.Documento,
            PadraoChave = "^rg$|^rg\\d*$|registrogeral|identidade",
            Mascara = TipoMascara.Parcial
        },
        new()
        {
            Nome = "CartaoSUS", Categoria = CategoriaPii.Documento,
            PadraoChave = "^cns$|cartaosus|numerosus",
            PadraoValor = @"(?<!\d)[1-9]\d{2}\s?\d{4}\s?\d{4}\s?\d{4}(?!\d)",
            ValorExigeChave = true, Mascara = TipoMascara.Parcial
        },
        new()
        {
            Nome = "Saude", Categoria = CategoriaPii.Saude,
            PadraoChave = "saude|laudo|diagnostic|condicao|^cid$|^cid10$|codigocid|medicament|doenca|deficiencia|alergia|tratamento|prontuario",
            Mascara = TipoMascara.Total, Rotulo = "dado de saúde protegido"
        },
        new()
        {
            // Dado de saúde escondido num campo de nome inocente ("observacao", "mensagem"...).
            Nome = "SaudeTextoLivre", Categoria = CategoriaPii.Saude,
            PadraoValor = @"(?i)\b(oxig[eê]nio|acamad[oa]s?|hemodi[aá]lise|di[aá]lise|insulina|marca-?passo|quimioterapia|" +
                          @"radioterapia|cadeirante|HIV|c[aâ]ncer|ventila[cç][aã]o mec[aâ]nica|traqueostomia|" +
                          @"CID[- ]?[A-TV-Z]\d{2}(\.\d)?)\b",
            Mascara = TipoMascara.Total, Rotulo = "dado de saúde protegido", MascararValorInteiro = true
        },
        new()
        {
            Nome = "Senha", Categoria = CategoriaPii.Credencial,
            PadraoChave = "senha|password|passwd|^pwd$|secret|apikey|^token$|accesstoken|refreshtoken",
            Mascara = TipoMascara.Total, Rotulo = "credencial protegida"
        },
        new()
        {
            // Credenciais reconhecíveis pelo formato: JWT e chaves de API.
            Nome = "TokenOuChave", Categoria = CategoriaPii.Credencial,
            PadraoValor = @"\beyJ[\w-]{8,}\.[\w-]{8,}\.[\w-]{8,}\b|\bsk-ant-[\w-]{10,}",
            Mascara = TipoMascara.Total, Rotulo = "credencial protegida"
        },
        new()
        {
            Nome = "Telefone", Categoria = CategoriaPii.Contato,
            PadraoChave = "telefone|celular|^fone|whatsapp",
            PadraoValor = @"\(\d{2}\)\s?9?\d{4}-?\d{4}",
            Mascara = TipoMascara.Telefone
        },
        new()
        {
            Nome = "Email", Categoria = CategoriaPii.Contato,
            PadraoValor = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}",
            Mascara = TipoMascara.Email
        },
    ];
}

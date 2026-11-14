using System.Text.Json.Nodes;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Privacidade;
using GlobalSolutionCitizenTech.Privacidade.Pii;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.UnitTests.Privacidade;

public class MotorPiiTests
{
    private const string CpfValido = "123.456.789-09";
    private static readonly MotorPii Motor = new(RegrasPadrao.Todas);

    [Theory]
    [InlineData("123.456.789-09", "***.456.***-09")]
    [InlineData("12345678909", "***.456.***-09")]
    public void Mascara_cpf_no_formato_do_enunciado(string cpf, string esperado) =>
        Assert.Equal(esperado, Mascaras.Cpf(cpf));

    [Theory]
    [InlineData("123.456.789-09", true)]
    [InlineData("529.982.247-25", true)]
    [InlineData("123.456.789-00", false)]
    [InlineData("111.111.111-11", false)]
    [InlineData("1234", false)]
    public void Valida_digito_verificador_do_cpf(string cpf, bool valido) => Assert.Equal(valido, Cpf.Valido(cpf));

    [Fact]
    public void Tagueia_e_mascara_o_payload_inteiro()
    {
        var json = JsonNode.Parse($$"""
            {
              "nome": "Maria", "cpf": "{{CpfValido}}", "rg": "12.345.678-9",
              "telefone": "(11) 98765-4321", "condicaoSaude": "Acamada, usa oxigênio",
              "credenciais": { "senha": "Enchente@2026" },
              "contatos": [ { "email": "maria@exemplo.com.br" } ]
            }
            """);

        var achados = Motor.MascararJson(json);

        Assert.Contains(achados, a => a.Regra == "CPF" && a.Categoria == CategoriaPii.Documento);
        Assert.Contains(achados, a => a.Regra == "Saude" && a.Categoria == CategoriaPii.Saude);
        Assert.Contains(achados, a => a.Regra == "Senha" && a.Categoria == CategoriaPii.Credencial);
        Assert.Contains(achados, a => a.Regra == "Email");

        Assert.Equal("***.456.***-09", (string)json!["cpf"]!);
        Assert.Equal("[dado de saúde protegido]", (string)json["condicaoSaude"]!);
        Assert.StartsWith("[", (string)json["credenciais"]!["senha"]!);
        Assert.Equal("Maria", (string)json["nome"]!);
        var texto = json.ToJsonString();
        Assert.DoesNotContain("789", texto);
        Assert.DoesNotContain("oxigênio", texto);
        Assert.DoesNotContain("Enchente", texto);
        Assert.DoesNotContain("98765", texto);
    }

    [Fact]
    public void Acha_cpf_e_saude_em_texto_livre()
    {
        var achados = Motor.AnalisarTexto($"Morador {CpfValido} depende de hemodiálise");
        Assert.Contains(achados, a => a.Regra == "CPF");
        Assert.Contains(achados, a => a.Categoria == CategoriaPii.Saude);
    }

    [Fact]
    public void Acha_token_jwt_e_chave_da_anthropic_em_texto()
    {
        Assert.Contains(Motor.AnalisarTexto("Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTYifQ.abcdefghijk"),
            a => a.Categoria == CategoriaPii.Credencial);
        Assert.Contains(Motor.AnalisarTexto("chave sk-ant-api03-abcdefghijklmnop"), a => a.Categoria == CategoriaPii.Credencial);
    }

    [Theory]
    [InlineData("cidade", "São Paulo")]                                   // "cid" não é "cidade"
    [InlineData("status", "Ativo")]                                       // "sus" não é "status"
    [InlineData("id", "0b1e6a2f-1234-4567-8901-123456789012")]            // fragmento de GUID não é CPF
    [InlineData("cpfMascarado", "***.456.***-09")]                        // já mascarado
    [InlineData("telefoneMascarado", "(11) *****-4321")]
    [InlineData("quantidade", "12345678900")]                             // 11 dígitos, mas CPF inválido
    public void Nao_gera_falso_positivo(string chave, string valor) =>
        Assert.Empty(Motor.AnalisarCampo("$." + chave, chave, valor));

    [Fact]
    public void Booleano_nao_e_dado_pessoal() =>
        Assert.Empty(Motor.AnalisarJson(JsonNode.Parse("""{ "possuiCondicaoSaude": true }""")));

    [Fact]
    public void Item_de_array_herda_o_nome_do_campo()
    {
        var achados = Motor.AnalisarJson(JsonNode.Parse("""{ "laudos": ["Diabetes tipo 1", "Asma"] }"""));
        Assert.Equal(2, achados.Count);
        Assert.All(achados, a => Assert.Equal(CategoriaPii.Saude, a.Categoria));
        Assert.Equal("$.laudos[1]", achados[1].Caminho);
    }

    [Fact]
    public void Regras_vem_da_configuracao()
    {
        var config = new ConfiguracaoPrivacidade
        {
            RegrasDesativadas = ["Email"],
            RegrasAdicionais =
            [
                new RegraPii { Nome = "NIS", Categoria = CategoriaPii.Documento, PadraoChave = "^nis$", Mascara = TipoMascara.Parcial }
            ]
        };
        var motor = new MotorPii(Options.Create(config));

        Assert.Empty(motor.AnalisarTexto("maria@exemplo.com.br"));
        Assert.Contains(motor.AnalisarCampo("$.nis", "nis", "12345678901"), a => a.Regra == "NIS");
    }

    [Fact]
    public void Regex_hostil_nao_trava_a_api()
    {
        var motor = new MotorPii([new RegraPii { Nome = "Hostil", Categoria = CategoriaPii.Documento, PadraoValor = "(a+)+$" }]);
        var inicio = DateTime.UtcNow;
        motor.AnalisarTexto(new string('a', 40) + "!");
        Assert.True(DateTime.UtcNow - inicio < TimeSpan.FromSeconds(2));
    }
}

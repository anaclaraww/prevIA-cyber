using System.Security.Cryptography;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Privacidade;
using GlobalSolutionCitizenTech.Privacidade.Ia;
using GlobalSolutionCitizenTech.Privacidade.Pii;
using GlobalSolutionCitizenTech.Privacidade.Protecao;
using GlobalSolutionCitizenTech.Privacidade.Taint;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.UnitTests.Privacidade;

public class ProtetorDadosAesTests
{
    public static ProtetorDadosAes Novo() => new(Options.Create(new ConfiguracaoPrivacidade
    {
        ChaveCriptografia = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ChaveHash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    }));

    [Fact]
    public void Cifra_e_decifra()
    {
        var p = Novo();
        var cifrado = p.Cifrar("Acamada, usa oxigênio");
        Assert.StartsWith("v1:", cifrado);
        Assert.DoesNotContain("oxig", cifrado);
        Assert.Equal("Acamada, usa oxigênio", p.Decifrar(cifrado));
    }

    [Fact]
    public void Mesmo_texto_gera_cifras_diferentes() =>
        Assert.NotEqual(Novo().Cifrar("11987654321"), Novo().Cifrar("11987654321"));

    [Fact]
    public void Valor_adulterado_no_banco_falha_em_vez_de_virar_lixo()
    {
        var p = Novo();
        var bytes = Convert.FromBase64String(p.Cifrar("11987654321")[3..]);
        bytes[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => p.Decifrar("v1:" + Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Hash_e_deterministico_e_depende_da_chave()
    {
        var p = Novo();
        Assert.Equal(p.Hash("12345678909"), p.Hash("12345678909"));
        Assert.NotEqual(p.Hash("12345678909"), Novo().Hash("12345678909"));
        Assert.Equal(64, p.Hash("12345678909").Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-base64!")]
    [InlineData("AAAA")]
    public void Chave_ausente_ou_invalida_falha_no_startup(string chave) =>
        Assert.Throws<InvalidOperationException>(() => new ProtetorDadosAes(Options.Create(new ConfiguracaoPrivacidade
        {
            ChaveCriptografia = chave,
            ChaveHash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        })));
}

public class ContextoTaintTests
{
    private static readonly MotorPii Motor = new(RegrasPadrao.Todas);

    [Fact]
    public void Valor_marcado_e_achado_com_e_sem_pontuacao()
    {
        using var _ = ContextoTaint.Iniciar("POST /teste", "t1");
        ContextoTaint.Atual!.Marcar(Motor.AnalisarCampo("$.cpf", "cpf", "123.456.789-09"));

        Assert.NotEmpty(ContextoTaint.Atual.Encontrar("erro com 123.456.789-09"));
        Assert.NotEmpty(ContextoTaint.Atual.Encontrar("erro com 12345678909"));
        Assert.Empty(ContextoTaint.Atual.Encontrar("nada aqui"));
    }

    [Fact]
    public void Valor_curto_nao_contamina_tudo()
    {
        using var _ = ContextoTaint.Iniciar("POST /teste", null);
        ContextoTaint.Atual!.Marcar(Motor.AnalisarCampo("$.senha", "senha", "abc"));
        Assert.True(ContextoTaint.Atual.Vazio);
    }

    [Fact]
    public async Task Contexto_nao_vaza_entre_requisicoes()
    {
        async Task<bool> Requisicao(string valor, string procurar)
        {
            using var _ = ContextoTaint.Iniciar("req", null);
            ContextoTaint.Atual!.Marcar(Motor.AnalisarCampo("$.senha", "senha", valor));
            await Task.Yield();
            return ContextoTaint.Atual.Encontrar(procurar).Count > 0;
        }

        var resultados = await Task.WhenAll(Requisicao("SenhaDaAna123", "SenhaDoBeto456"), Requisicao("SenhaDoBeto456", "SenhaDaAna123"));
        Assert.All(resultados, Assert.False);
        Assert.Null(ContextoTaint.Atual);
    }
}

public class RiscoCidadaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TipoEventoSeguranca.PiiProtegida, CategoriaPii.Saude, Farol.Verde)]
    [InlineData(TipoEventoSeguranca.VazamentoBloqueadoLog, CategoriaPii.Saude, Farol.Vermelho)]
    [InlineData(TipoEventoSeguranca.VazamentoBloqueadoLog, CategoriaPii.Documento, Farol.Amarelo)]
    [InlineData(TipoEventoSeguranca.VazamentoMascaradoResposta, CategoriaPii.Contato, Farol.Amarelo)]
    [InlineData(TipoEventoSeguranca.VazamentoMascaradoResposta, CategoriaPii.Credencial, Farol.Vermelho)]
    [InlineData(TipoEventoSeguranca.VazamentoBloqueadoResposta, CategoriaPii.Documento, Farol.Vermelho)]
    public void Classifica_o_farol_por_regra(TipoEventoSeguranca tipo, CategoriaPii categoria, Farol esperado) =>
        Assert.Equal(esperado, RiscoCidadao.Classificar(tipo, [categoria]));

    private static EventoSeguranca Evento(Farol farol, TimeSpan atras) =>
        new(TipoEventoSeguranca.VazamentoBloqueadoLog, SinkSeguranca.Log, farol, [CategoriaPii.Documento], "d", "o", null, Agora - atras);

    [Fact]
    public void Sem_incidente_o_indicador_e_verde() =>
        Assert.Equal((0, Farol.Verde), RiscoCidadao.Consolidar([Evento(Farol.Verde, TimeSpan.Zero)], Agora));

    [Fact]
    public void Dois_amarelos_acendem_o_amarelo() =>
        Assert.Equal((16, Farol.Amarelo), RiscoCidadao.Consolidar([Evento(Farol.Amarelo, TimeSpan.Zero), Evento(Farol.Amarelo, TimeSpan.FromMinutes(5))], Agora));

    [Fact]
    public void Dois_vermelhos_acendem_o_vermelho_e_o_teto_e_100()
    {
        Assert.Equal((50, Farol.Vermelho), RiscoCidadao.Consolidar([Evento(Farol.Vermelho, TimeSpan.Zero), Evento(Farol.Vermelho, TimeSpan.Zero)], Agora));
        Assert.Equal(100, RiscoCidadao.Consolidar(Enumerable.Range(0, 9).Select(_ => Evento(Farol.Vermelho, TimeSpan.Zero)), Agora).Indicador);
    }

    [Fact]
    public void Evento_fora_da_janela_nao_conta() =>
        Assert.Equal(0, RiscoCidadao.Consolidar([Evento(Farol.Vermelho, TimeSpan.FromMinutes(61))], Agora).Indicador);
}

public class TradutorRiscoTests
{
    private static readonly MotorPii Motor = new(RegrasPadrao.Todas);

    private static EventoSeguranca Evento(TipoEventoSeguranca tipo, CategoriaPii categoria) =>
        new(tipo, SinkSeguranca.Log, Farol.Vermelho, [categoria],
            "Log Information em Demo tentou registrar Saude (linha suprimida). Detecção: log.CondicaoSaude.",
            "POST /api/v1/demonstracao/vazamentos/log-saude", "t1", DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(TipoEventoSeguranca.PiiProtegida, CategoriaPii.Saude)]
    [InlineData(TipoEventoSeguranca.VazamentoBloqueadoLog, CategoriaPii.Credencial)]
    [InlineData(TipoEventoSeguranca.VazamentoMascaradoResposta, CategoriaPii.Documento)]
    [InlineData(TipoEventoSeguranca.VazamentoBloqueadoResposta, CategoriaPii.Contato)]
    public void Texto_offline_tem_duas_frases_e_nenhum_jargao(TipoEventoSeguranca tipo, CategoriaPii categoria)
    {
        var t = TradutorRiscoOffline.Traduzir(Evento(tipo, categoria));
        Assert.Equal("offline", t.TraduzidoPor);
        Assert.Equal(2, t.Mensagem.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length);
        foreach (var jargao in new[] { "log ", "HTTP", "PII", "taint", "payload" })
            Assert.DoesNotContain(jargao, t.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_para_a_ia_segue_o_enunciado_e_nao_tem_dado_pessoal()
    {
        var prompt = TradutorRiscoClaude.MontarPrompt(Evento(TipoEventoSeguranca.VazamentoBloqueadoLog, CategoriaPii.Saude));
        Assert.Contains("cidadão leigo de 60 anos, em 2 frases", prompt);
        Assert.Contains("condição de saúde", prompt);
        Assert.Empty(Motor.AnalisarTexto(prompt));
    }
}

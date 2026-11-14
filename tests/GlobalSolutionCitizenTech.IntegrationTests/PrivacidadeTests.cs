using System.Net;
using System.Text.Json;
using GlobalSolutionCitizenTech.Application.Dtos;
using GlobalSolutionCitizenTech.Application.Privacidade;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Infrastructure.Persistencia;
using GlobalSolutionCitizenTech.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalSolutionCitizenTech.IntegrationTests;

/// <summary>
/// Auditoria do taint analysis com a API inteira no ar: source (payload), sinks (banco, log, resposta HTTP)
/// e o painel Risco Cidadão.
/// </summary>
[Collection(ApiCollection.Nome)]
public class PrivacidadeTests(ApiFactory api)
{
    private const string Demo = "/api/v1/demonstracao/vazamentos/";
    private const string Telefone = "(11) 98765-4321";
    private const string Saude = "Acamada, usa concentrador de oxigênio";

    private static string CpfNovo()
    {
        var d = Enumerable.Range(0, 9).Select(_ => Random.Shared.Next(10)).ToList();
        foreach (var peso in new[] { 10, 11 })
        {
            var resto = d.Select((x, i) => x * (peso - i)).Sum() * 10 % 11;
            d.Add(resto == 10 ? 0 : resto);
        }
        var s = string.Concat(d);
        return $"{s[..3]}.{s[3..6]}.{s[6..9]}-{s[9..]}";
    }

    private async Task<Guid> BairroAsync()
    {
        var r = await api.Cliente().PostJsonAsync("/api/v1/bairros", new
        {
            nome = $"Priv {Guid.NewGuid():N}"[..18], cidade = "São Paulo", uf = "SP", latitude = -23.5, longitude = -46.6, limiarChuvaMm = 20
        });
        return (await r.LerAsync<BairroResponse>()).Id;
    }

    private void SemVazamentoNosLogs(params string[] valores)
    {
        var logs = api.Logs.Linhas;
        foreach (var v in valores)
            Assert.DoesNotContain(logs, l => l.Contains(v, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cadastro_grava_so_mascara_hash_e_cifra()
    {
        var cpf = CpfNovo();
        var r = await api.Cliente("Operador").PostJsonAsync("/api/v1/moradores", new
        {
            nome = "Dona Maria", bairroId = await BairroAsync(), cpf, rg = "12.345.678-9",
            telefone = Telefone, condicaoSaude = Saude, prioridade = "Critica"
        });

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain(cpf, corpo);
        Assert.DoesNotContain("98765", corpo);
        Assert.DoesNotContain("oxigênio", corpo);
        var morador = await r.LerAsync<MoradorResponse>();
        Assert.Equal($"***.{cpf[4..7]}.***-{cpf[^2..]}", morador.CpfMascarado);
        Assert.True(morador.PossuiCondicaoSaude);

        // Banco: nenhuma coluna guarda o dado em claro.
        using var escopo = api.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var linha = await db.Moradores.AsNoTracking().SingleAsync(m => m.Id == morador.Id);
        var gravado = JsonSerializer.Serialize(linha);
        Assert.DoesNotContain(cpf, gravado);
        Assert.DoesNotContain(cpf.Replace(".", "").Replace("-", ""), gravado);
        Assert.DoesNotContain("98765", gravado);
        Assert.DoesNotContain("oxigênio", gravado);
        Assert.StartsWith("v1:", linha.TelefoneCifrado);
        Assert.StartsWith("v1:", linha.CondicaoSaudeCifrada);
        Assert.Equal(64, linha.CpfHash.Length);

        SemVazamentoNosLogs(cpf, "98765", "oxigênio");
    }

    [Fact]
    public async Task Cpf_repetido_e_recusado_sem_ecoar_o_numero()
    {
        var cpf = CpfNovo();
        var bairro = await BairroAsync();
        object Corpo() => new { nome = "Seu José", bairroId = bairro, cpf, telefone = Telefone };

        Assert.Equal(HttpStatusCode.Created, (await api.Cliente().PostJsonAsync("/api/v1/moradores", Corpo())).StatusCode);
        var r = await api.Cliente().PostJsonAsync("/api/v1/moradores", Corpo());

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.DoesNotContain(cpf, await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Equipe_de_resgate_ve_o_contato_e_ninguem_mais()
    {
        var cpf = CpfNovo();
        var criado = await (await api.Cliente().PostJsonAsync("/api/v1/moradores", new
        {
            nome = "Dona Lurdes", bairroId = await BairroAsync(), cpf, telefone = Telefone, condicaoSaude = Saude
        })).LerAsync<MoradorResponse>();
        var url = $"/api/v1/moradores/{criado.Id}/contato-emergencia";

        var contato = await (await api.Cliente("Operador").GetAsync(url)).LerAsync<ContatoEmergenciaResponse>();
        Assert.Equal(Telefone, contato.Telefone);
        Assert.Equal(Saude, contato.CondicaoSaude);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Cliente("Sensor").GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Log_com_dado_de_saude_e_bloqueado()
    {
        var r = await api.Cliente().PostJsonAsync(Demo + "log-saude", new { nome = "Seu José", condicaoSaude = "Faz hemodiálise às terças" });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains(api.Logs.Linhas, l => l.Contains("[LOG BLOQUEADO pelo taint analysis") && l.Contains("DemonstracaoVazamentos"));
        SemVazamentoNosLogs("hemodiálise");
    }

    [Fact]
    public async Task Senha_interpolada_no_log_e_bloqueada_pelo_taint()
    {
        var r = await api.Cliente().PostJsonAsync(Demo + "senha-no-log", new { usuario = "operador.sul", senha = "Enchente@2026!" });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        SemVazamentoNosLogs("Enchente@2026!");
    }

    [Fact]
    public async Task Erro_que_ecoa_o_cpf_sai_mascarado_na_resposta_e_no_log()
    {
        var cpf = CpfNovo();
        var r = await api.Cliente().PostJsonAsync(Demo + "erro-com-cpf", new { cpf });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain(cpf, corpo);
        Assert.Contains($"***.{cpf[4..7]}.***-{cpf[^2..]}", corpo);
        Assert.Contains("VALIDATION_ERROR", corpo);
        SemVazamentoNosLogs(cpf);
    }

    [Fact]
    public async Task Resposta_anonima_com_dado_pessoal_e_barrada()
    {
        var cpf = CpfNovo();
        var r = await api.CreateClient().GetAsync(Demo + "consulta-publica?cpf=" + Uri.EscapeDataString(cpf));

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.Contains("SENSITIVE_DATA_BLOCKED", corpo);
        Assert.DoesNotContain(cpf, corpo);
        Assert.DoesNotContain("oxigênio", corpo);
        Assert.DoesNotContain("98765", corpo);
    }

    [Fact]
    public async Task Login_continua_devolvendo_o_token_mas_nao_loga_o_segredo()
    {
        var (id, segredo) = ApiFactory.Credenciais["Operador"];
        var r = await api.CreateClient().PostJsonAsync("/api/v1/auth/token", new { clientId = id, clientSecret = segredo });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.StartsWith("eyJ", (await r.LerAsync<JsonElement>()).GetProperty("accessToken").GetString());
        SemVazamentoNosLogs(segredo);
    }

    [Fact]
    public async Task Painel_mostra_farol_indicador_e_mensagem_ao_cidadao()
    {
        await api.Cliente().PostJsonAsync(Demo + "log-saude", new { nome = "Dona Ana", condicaoSaude = "Usa insulina" });
        await api.CreateClient().GetAsync(Demo + "consulta-publica?cpf=" + Uri.EscapeDataString(CpfNovo()));

        // Gravação e tradução rodam em background: espera até os eventos chegarem traduzidos.
        PainelRiscoCidadaoResponse painel = null!;
        for (var i = 0; i < 50; i++)
        {
            painel = await (await api.Cliente("Operador").GetAsync("/api/v1/seguranca/painel")).LerAsync<PainelRiscoCidadaoResponse>();
            if (painel.Vermelhos >= 2 && painel.Eventos.All(e => e.MensagemCidadao is not null))
                break;
            await Task.Delay(100);
        }

        Assert.Equal(Farol.Vermelho, painel.Farol);
        Assert.InRange(painel.IndicadorRiscoCidadao, 50, 100);
        Assert.NotEqual(PainelSegurancaService.MensagemSemRisco, painel.MensagemPrincipal);
        Assert.Contains(painel.Eventos, e => e.Tipo == TipoEventoSeguranca.VazamentoBloqueadoLog && e.Categorias.Contains(CategoriaPii.Saude));
        Assert.Contains(painel.Eventos, e => e.Tipo == TipoEventoSeguranca.VazamentoBloqueadoResposta);
        Assert.All(painel.Eventos, e => Assert.Equal("offline", e.TraduzidoPor));

        var json = JsonSerializer.Serialize(painel);
        Assert.DoesNotContain("insulina", json);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/v1/seguranca/painel")).StatusCode);
    }

    [Fact]
    public async Task Texto_que_nao_e_utf8_vira_400_e_nao_500()
    {
        // "José" em Latin-1 (é = 0xE9), como o Git Bash do Windows envia.
        var corpo = new ByteArrayContent([.. "{\"nome\":\"Jos"u8, 0xE9, .. "\",\"condicaoSaude\":\"x\"}"u8]);
        corpo.Headers.ContentType = new("application/json");

        var r = await api.Cliente().PostAsync(Demo + "log-saude", corpo);

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Pagina_do_painel_e_servida_pela_api()
    {
        var r = await api.CreateClient().GetAsync("/painel/");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Risco Cidadão", await r.Content.ReadAsStringAsync());
    }
}

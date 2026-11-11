using System.Text.Json;
using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Excecoes;

namespace GlobalSolutionCitizenTech.Infrastructure.Clientes;

/// <summary>Consulta de endereço por CEP via ViaCEP (https://viacep.com.br).</summary>
public class ViaCepClient(HttpClient http) : ICepClient
{
    public async Task<EnderecoCep?> ConsultarAsync(string cep, CancellationToken ct)
    {
        try
        {
            using var resposta = await http.GetAsync($"ws/{cep}/json/", ct);
            if (resposta.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return null; // formato inválido

            resposta.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var raiz = json.RootElement;

            // CEP inexistente: o ViaCEP responde 200 com {"erro": true} (ou "true", nas versões novas).
            if (raiz.TryGetProperty("erro", out _))
                return null;

            return new EnderecoCep(
                Texto(raiz, "cep"), Texto(raiz, "logradouro"), Texto(raiz, "bairro"),
                Texto(raiz, "localidade"), Texto(raiz, "uf"));
        }
        catch (Exception ex) when (ex is not ServicoExternoException && !ct.IsCancellationRequested)
        {
            throw new ServicoExternoException("ViaCEP", ex.Message, ex);
        }
    }

    private static string Texto(JsonElement raiz, string nome) =>
        raiz.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString()! : "";
}

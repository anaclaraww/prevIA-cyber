using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Excecoes;

namespace GlobalSolutionCitizenTech.Infrastructure.Clientes;

/// <summary>
/// Previsão horária de precipitação via Open-Meteo (https://open-meteo.com), gratuita e sem chave.
/// Soma a chuva prevista das próximas N horas.
/// </summary>
public class OpenMeteoClient(HttpClient http) : IPrevisaoTempoClient
{
    public async Task<decimal> ObterChuvaPrevistaMmAsync(double latitude, double longitude, int horas, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={latitude}&longitude={longitude}&hourly=precipitation&forecast_hours={horas}&timezone=UTC");

        try
        {
            var resposta = await http.GetFromJsonAsync<RespostaOpenMeteo>(url, ct)
                           ?? throw new ServicoExternoException("Open-Meteo", "resposta vazia.");
            var precipitacoes = resposta.Hourly?.Precipitation
                                ?? throw new ServicoExternoException("Open-Meteo", "resposta sem dados de precipitação.");

            return Math.Round(precipitacoes.Take(horas).Sum(p => p ?? 0m), 2);
        }
        catch (Exception ex) when (ex is not ServicoExternoException && !ct.IsCancellationRequested)
        {
            throw new ServicoExternoException("Open-Meteo", ex.Message, ex);
        }
    }

    private sealed record RespostaOpenMeteo([property: JsonPropertyName("hourly")] DadosHorarios? Hourly);

    private sealed record DadosHorarios(
        [property: JsonPropertyName("time")] List<string>? Time,
        [property: JsonPropertyName("precipitation")] List<decimal?>? Precipitation);
}

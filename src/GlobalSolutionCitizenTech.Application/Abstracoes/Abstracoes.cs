using GlobalSolutionCitizenTech.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Application.Abstracoes;

/// <summary>Porta de acesso ao banco. Implementada pelo EF Core na camada de infraestrutura.</summary>
public interface IAppDbContext
{
    DbSet<Bairro> Bairros { get; }
    DbSet<Quadra> Quadras { get; }
    DbSet<Sensor> Sensores { get; }
    DbSet<Leitura> Leituras { get; }
    DbSet<Robo> Robos { get; }
    DbSet<Missao> Missoes { get; }
    DbSet<Alerta> Alertas { get; }
    DbSet<AvaliacaoRisco> AvaliacoesRisco { get; }
    DbSet<Morador> Moradores { get; }
    DbSet<EventoSeguranca> EventosSeguranca { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Previsão de chuva (implementação: API Open-Meteo).</summary>
public interface IPrevisaoTempoClient
{
    /// <summary>Soma da precipitação prevista (mm) nas próximas <paramref name="horas"/> horas.</summary>
    Task<decimal> ObterChuvaPrevistaMmAsync(double latitude, double longitude, int horas, CancellationToken ct);
}

public record EnderecoCep(string Cep, string Logradouro, string Bairro, string Cidade, string Uf);

/// <summary>Consulta de endereço por CEP (implementação: API ViaCEP).</summary>
public interface ICepClient
{
    /// <returns>O endereço, ou <c>null</c> quando o CEP não existe.</returns>
    Task<EnderecoCep?> ConsultarAsync(string cep, CancellationToken ct);
}

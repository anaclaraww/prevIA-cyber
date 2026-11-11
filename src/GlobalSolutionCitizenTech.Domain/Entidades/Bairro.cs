using GlobalSolutionCitizenTech.Domain.Comum;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>Região monitorada. As coordenadas são usadas para consultar a previsão do tempo.</summary>
public class Bairro : Entidade
{
    public string Nome { get; private set; } = null!;
    public string Cidade { get; private set; } = null!;
    public string Uf { get; private set; } = null!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    /// <summary>Chuva prevista (mm nas próximas horas) a partir da qual o bairro alaga.</summary>
    public decimal LimiarChuvaMm { get; private set; }

    private Bairro() { }

    public Bairro(string nome, string cidade, string uf, double latitude, double longitude, decimal limiarChuvaMm)
    {
        Atualizar(nome, cidade, uf, latitude, longitude, limiarChuvaMm);
    }

    public void Atualizar(string nome, string cidade, string uf, double latitude, double longitude, decimal limiarChuvaMm)
    {
        if (limiarChuvaMm <= 0)
            throw new RegraNegocioException("O limiar de chuva do bairro deve ser maior que zero.");

        Nome = nome.Trim();
        Cidade = cidade.Trim();
        Uf = uf.Trim().ToUpperInvariant();
        Latitude = latitude;
        Longitude = longitude;
        LimiarChuvaMm = limiarChuvaMm;
    }
}

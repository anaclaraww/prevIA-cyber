using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Domain.Entidades;

/// <summary>Sensor IoT instalado em uma boca de lobo, um pluviômetro ou uma quadra.</summary>
public class Sensor : Entidade
{
    public string Codigo { get; private set; } = null!;
    public TipoSensor Tipo { get; private set; }
    public Guid BairroId { get; private set; }
    public Bairro? Bairro { get; private set; }
    public Guid? QuadraId { get; private set; }
    public Quadra? Quadra { get; private set; }

    /// <summary>Valor a partir do qual a leitura dispara uma ação (ex.: % de obstrução da boca de lobo).</summary>
    public decimal? Limiar { get; private set; }
    public bool Ativo { get; private set; } = true;
    public decimal? UltimoValor { get; private set; }
    public DateTimeOffset? UltimaLeituraEm { get; private set; }

    public string Unidade => UnidadeDe(Tipo);

    private Sensor() { }

    public Sensor(string codigo, TipoSensor tipo, Guid bairroId, Guid? quadraId, decimal? limiar)
    {
        if (tipo == TipoSensor.NivelQuadra && quadraId is null)
            throw new RegraNegocioException("Sensor de nível precisa estar vinculado a uma quadra.");
        if (tipo == TipoSensor.BocaDeLobo && limiar is null or <= 0 or > 100)
            throw new RegraNegocioException("Sensor de boca de lobo precisa de um limiar de obstrução entre 0 e 100%.");

        Codigo = codigo.Trim().ToUpperInvariant();
        Tipo = tipo;
        BairroId = bairroId;
        QuadraId = quadraId;
        Limiar = limiar;
    }

    public bool UltrapassaLimiar(decimal valor) => Limiar is not null && valor >= Limiar;

    public Leitura RegistrarLeitura(decimal valor, DateTimeOffset momento)
    {
        if (!Ativo)
            throw new RegraNegocioException($"O sensor '{Codigo}' está inativo e não aceita leituras.");
        if (valor < 0)
            throw new RegraNegocioException("O valor da leitura não pode ser negativo.");
        if (Tipo is TipoSensor.BocaDeLobo or TipoSensor.NivelQuadra && valor > 100)
            throw new RegraNegocioException($"Leitura de {Tipo} é percentual e deve estar entre 0 e 100.");

        UltimoValor = valor;
        UltimaLeituraEm = momento;
        return new Leitura(Id, valor, momento);
    }

    public void Desativar() => Ativo = false;
    public void Ativar() => Ativo = true;

    public static string UnidadeDe(TipoSensor tipo) => tipo switch
    {
        TipoSensor.BocaDeLobo => "% obstrução",
        TipoSensor.Pluviometro => "mm/h",
        TipoSensor.NivelQuadra => "% capacidade",
        _ => ""
    };
}

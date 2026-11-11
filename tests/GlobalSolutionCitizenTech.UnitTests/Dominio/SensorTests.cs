using GlobalSolutionCitizenTech.Domain.Comum;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.UnitTests.Dominio;

public class SensorTests
{
    private static readonly Guid Bairro = Guid.NewGuid();

    [Fact]
    public void BocaDeLobo_SemLimiar_Lanca() =>
        Assert.Throws<RegraNegocioException>(() => new Sensor("BL-1", TipoSensor.BocaDeLobo, Bairro, null, null));

    [Fact]
    public void NivelQuadra_SemQuadra_Lanca() =>
        Assert.Throws<RegraNegocioException>(() => new Sensor("NIV-1", TipoSensor.NivelQuadra, Bairro, null, null));

    [Fact]
    public void RegistrarLeitura_AtualizaUltimoValor()
    {
        var sensor = new Sensor("bl-1", TipoSensor.BocaDeLobo, Bairro, null, 70);
        var agora = DateTimeOffset.UtcNow;

        var leitura = sensor.RegistrarLeitura(42, agora);

        Assert.Equal("BL-1", sensor.Codigo);
        Assert.Equal(42, sensor.UltimoValor);
        Assert.Equal(agora, sensor.UltimaLeituraEm);
        Assert.Equal(sensor.Id, leitura.SensorId);
    }

    [Theory]
    [InlineData(69.9, false)]
    [InlineData(70, true)]
    [InlineData(95, true)]
    public void UltrapassaLimiar(decimal valor, bool esperado) =>
        Assert.Equal(esperado, new Sensor("BL-1", TipoSensor.BocaDeLobo, Bairro, null, 70).UltrapassaLimiar(valor));

    [Fact]
    public void RegistrarLeitura_SensorInativo_Lanca()
    {
        var sensor = new Sensor("PLU-1", TipoSensor.Pluviometro, Bairro, null, null);
        sensor.Desativar();

        Assert.Throws<RegraNegocioException>(() => sensor.RegistrarLeitura(5, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RegistrarLeitura_PercentualAcimaDe100_Lanca()
    {
        var sensor = new Sensor("BL-1", TipoSensor.BocaDeLobo, Bairro, null, 70);

        Assert.Throws<RegraNegocioException>(() => sensor.RegistrarLeitura(120, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RegistrarLeitura_PluviometroAceitaAcimaDe100()
    {
        var sensor = new Sensor("PLU-1", TipoSensor.Pluviometro, Bairro, null, null);

        sensor.RegistrarLeitura(150, DateTimeOffset.UtcNow);

        Assert.Equal(150, sensor.UltimoValor);
    }
}

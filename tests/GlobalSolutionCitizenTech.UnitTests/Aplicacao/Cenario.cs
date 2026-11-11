using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Application.Excecoes;
using GlobalSolutionCitizenTech.Application.Servicos;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using GlobalSolutionCitizenTech.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.UnitTests.Aplicacao;

/// <summary>Banco em memória com um bairro, uma quadra, sensores e robôs, mais os serviços prontos.</summary>
public sealed class Cenario : IDisposable
{
    public AppDbContext Db { get; }
    public PrevisaoFalsa Previsao { get; } = new();
    public Bairro Bairro { get; }
    public Quadra Quadra { get; }
    public Sensor BocaDeLobo1 { get; }
    public Sensor BocaDeLobo2 { get; }
    public Sensor NivelQuadra { get; }

    public Cenario(int bateriaDesentupidor = 90, bool comDesentupidor = true)
    {
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        Bairro = new Bairro("Bairro Teste", "São Paulo", "SP", -23.5, -46.6, 20m);
        Quadra = new Quadra("Quadra Teste", Bairro.Id, "01310100", "Av. Paulista", "1", -23.5, -46.6, 400m);
        BocaDeLobo1 = new Sensor("BL-1", TipoSensor.BocaDeLobo, Bairro.Id, null, 70m);
        BocaDeLobo2 = new Sensor("BL-2", TipoSensor.BocaDeLobo, Bairro.Id, null, 70m);
        NivelQuadra = new Sensor("NIV-1", TipoSensor.NivelQuadra, Bairro.Id, Quadra.Id, null);

        Db.AddRange(Bairro, Quadra, BocaDeLobo1, BocaDeLobo2, NivelQuadra);
        if (comDesentupidor)
            Db.Add(new Robo("DES-1", TipoRobo.Desentupidor, Bairro.Id, bateriaDesentupidor));
        Db.SaveChanges();
    }

    public IDespachoMissoes Despacho => new DespachoMissoes(Db, TimeProvider.System);
    public SensorService Sensores => new(Db, Despacho, TimeProvider.System);
    public RiscoService Risco => new(Db, Previsao);
    public MissaoService Missoes => new(Db, Despacho, TimeProvider.System);
    public RoboService Robos => new(Db, Despacho);

    public void Dispose() => Db.Dispose();
}

public sealed class PrevisaoFalsa : IPrevisaoTempoClient
{
    public decimal ChuvaMm { get; set; }
    public bool Falhar { get; set; }

    public Task<decimal> ObterChuvaPrevistaMmAsync(double latitude, double longitude, int horas, CancellationToken ct) =>
        Falhar
            ? throw new ServicoExternoException("Open-Meteo", "indisponível (simulado).")
            : Task.FromResult(ChuvaMm);
}

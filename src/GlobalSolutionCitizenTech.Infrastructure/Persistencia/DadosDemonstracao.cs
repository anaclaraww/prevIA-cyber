using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Infrastructure.Persistencia;

/// <summary>
/// Popula o banco com dados de demonstração quando ele está vazio.
/// Bairros reais de São Paulo; quadras, sensores e robôs são fictícios.
/// </summary>
public static class DadosDemonstracao
{
    public static async Task PopularAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Bairros.AnyAsync(ct))
            return;

        var pantanal = new Bairro("Jardim Pantanal", "São Paulo", "SP", -23.4987, -46.4416, 20m);
        var leopoldina = new Bairro("Vila Leopoldina", "São Paulo", "SP", -23.5270, -46.7325, 30m);
        db.Bairros.AddRange(pantanal, leopoldina);

        var quadraPantanal = new Quadra("Quadra Poliesportiva Pantanal", pantanal.Id, "08142000",
            "Rua Exemplo do Pantanal", "100", -23.4990, -46.4420, 420m);
        var quadraPantanal2 = new Quadra("Quadra da Escola Municipal", pantanal.Id, "08142000",
            "Rua Exemplo do Pantanal", "350", -23.4979, -46.4402, 300m);
        var quadraLeopoldina = new Quadra("Quadra Leopoldina", leopoldina.Id, "05303000",
            "Rua Exemplo da Leopoldina", "50", -23.5268, -46.7330, 500m);
        db.Quadras.AddRange(quadraPantanal, quadraPantanal2, quadraLeopoldina);

        db.Sensores.AddRange(
            new Sensor("BL-PANT-01", TipoSensor.BocaDeLobo, pantanal.Id, null, 70m),
            new Sensor("BL-PANT-02", TipoSensor.BocaDeLobo, pantanal.Id, null, 70m),
            new Sensor("PLU-PANT-01", TipoSensor.Pluviometro, pantanal.Id, null, null),
            new Sensor("NIV-PANT-Q1", TipoSensor.NivelQuadra, pantanal.Id, quadraPantanal.Id, null),
            new Sensor("BL-LEO-01", TipoSensor.BocaDeLobo, leopoldina.Id, null, 75m),
            new Sensor("NIV-LEO-Q1", TipoSensor.NivelQuadra, leopoldina.Id, quadraLeopoldina.Id, null));

        db.Robos.AddRange(
            new Robo("DES-PANT-01", TipoRobo.Desentupidor, pantanal.Id, 90),
            new Robo("DES-PANT-02", TipoRobo.Desentupidor, pantanal.Id, 25),
            new Robo("INS-PANT-01", TipoRobo.Inspetor, pantanal.Id, 80),
            new Robo("DES-LEO-01", TipoRobo.Desentupidor, leopoldina.Id, 100));

        await db.SaveChangesAsync(ct);
    }
}

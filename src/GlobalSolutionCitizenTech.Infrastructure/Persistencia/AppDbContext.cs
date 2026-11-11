using GlobalSolutionCitizenTech.Application.Abstracoes;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlobalSolutionCitizenTech.Infrastructure.Persistencia;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Bairro> Bairros => Set<Bairro>();
    public DbSet<Quadra> Quadras => Set<Quadra>();
    public DbSet<Sensor> Sensores => Set<Sensor>();
    public DbSet<Leitura> Leituras => Set<Leitura>();
    public DbSet<Robo> Robos => Set<Robo>();
    public DbSet<Missao> Missoes => Set<Missao>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<AvaliacaoRisco> AvaliacoesRisco => Set<AvaliacaoRisco>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // Enums gravados como texto: o banco fica legível sem tabela de tradução.
        c.Properties<StatusQuadra>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<TipoSensor>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<TipoRobo>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<StatusRobo>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<TipoMissao>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<StatusMissao>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<PrioridadeMissao>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<NivelRisco>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<decimal>().HavePrecision(10, 2);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Bairro>(e =>
        {
            e.ToTable("bairros");
            e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            e.Property(x => x.Cidade).HasMaxLength(100).IsRequired();
            e.Property(x => x.Uf).HasMaxLength(2).IsRequired();
            e.HasIndex(x => new { x.Nome, x.Cidade }).IsUnique();
        });

        mb.Entity<Quadra>(e =>
        {
            e.ToTable("quadras");
            e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
            e.Property(x => x.Cep).HasMaxLength(8).IsRequired();
            e.Property(x => x.Logradouro).HasMaxLength(200).IsRequired();
            e.Property(x => x.Numero).HasMaxLength(10);
            e.Property(x => x.CapacidadeM3).HasPrecision(12, 2);
            e.Property(x => x.NivelAtualPercentual).HasPrecision(5, 2);
            e.Ignore(x => x.VolumeRetidoM3);
            e.Ignore(x => x.ProximaDaCapacidade);
            e.HasOne(x => x.Bairro).WithMany().HasForeignKey(x => x.BairroId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.BairroId, x.Status });
        });

        mb.Entity<Sensor>(e =>
        {
            e.ToTable("sensores");
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.Property(x => x.Limiar).HasPrecision(6, 2);
            e.Ignore(x => x.Unidade);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasOne(x => x.Bairro).WithMany().HasForeignKey(x => x.BairroId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Quadra).WithMany().HasForeignKey(x => x.QuadraId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Leitura>(e =>
        {
            e.ToTable("leituras");
            e.HasOne<Sensor>().WithMany().HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SensorId, x.RegistradaEm });
        });

        mb.Entity<Robo>(e =>
        {
            e.ToTable("robos");
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasOne(x => x.BairroBase).WithMany().HasForeignKey(x => x.BairroBaseId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Missao>(e =>
        {
            e.ToTable("missoes");
            e.Property(x => x.Descricao).HasMaxLength(300).IsRequired();
            e.Ignore(x => x.Aberta);
            e.HasOne<Bairro>().WithMany().HasForeignKey(x => x.BairroId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sensor).WithMany().HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Robo).WithMany().HasForeignKey(x => x.RoboId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.BairroId, x.Status });
        });

        mb.Entity<Alerta>(e =>
        {
            e.ToTable("alertas");
            e.Property(x => x.Mensagem).HasMaxLength(500).IsRequired();
            e.HasOne<Bairro>().WithMany().HasForeignKey(x => x.BairroId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Quadra>().WithMany().HasForeignKey(x => x.QuadraId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.BairroId, x.Ativo });
        });

        mb.Entity<AvaliacaoRisco>(e =>
        {
            e.ToTable("avaliacoes_risco");
            e.Property(x => x.ChuvaPrevistaMm).HasPrecision(8, 2);
            e.HasOne<Bairro>().WithMany().HasForeignKey(x => x.BairroId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.BairroId, x.CriadoEm });
        });
    }
}

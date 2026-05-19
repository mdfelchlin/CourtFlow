using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public class DivisionConfig : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> b)
    {
        b.ToTable("divisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Format).HasConversion<int>();
        b.Property(x => x.TiebreakerPreset).HasConversion<int>();
        b.Property(x => x.WaveId);
        b.HasIndex(x => x.WaveId);
        b.HasMany(x => x.Pools).WithOne().HasForeignKey(p => p.DivisionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Teams).WithOne().HasForeignKey(t => t.DivisionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TournamentId);
    }
}

public class PoolConfig : IEntityTypeConfiguration<Pool>
{
    public void Configure(EntityTypeBuilder<Pool> b)
    {
        b.ToTable("pools");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Sequence).HasDefaultValue(0);
        b.Property(x => x.TargetSize).HasDefaultValue(4);
        b.HasIndex(x => x.DivisionId);
        b.HasIndex(x => new { x.DivisionId, x.Sequence });
    }
}

public class TeamConfig : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("teams");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Club).HasMaxLength(200);
        b.Property(x => x.Wave).HasConversion<int>();
        b.HasIndex(x => x.DivisionId);
        b.HasIndex(x => x.PoolId);
    }
}

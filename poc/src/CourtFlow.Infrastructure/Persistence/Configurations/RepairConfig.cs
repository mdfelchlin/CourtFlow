using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public sealed class RepairJobConfig : IEntityTypeConfiguration<RepairJob>
{
    public void Configure(EntityTypeBuilder<RepairJob> b)
    {
        b.ToTable("repair_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.TriggerType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.HasMany(x => x.Actions).WithOne().HasForeignKey(a => a.RepairJobId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TournamentId);
    }
}

public sealed class RepairActionConfig : IEntityTypeConfiguration<RepairAction>
{
    public void Configure(EntityTypeBuilder<RepairAction> b)
    {
        b.ToTable("repair_actions");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.TargetId).HasMaxLength(200);
        b.HasIndex(x => x.RepairJobId);
    }
}

public sealed class CourtOutageConfig : IEntityTypeConfiguration<CourtOutage>
{
    public void Configure(EntityTypeBuilder<CourtOutage> b)
    {
        b.ToTable("court_outages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => x.CourtId);
        b.HasIndex(x => new { x.CourtId, x.StartsAt, x.EndsAt });
    }
}

public sealed class MatchOfficialsConfig : IEntityTypeConfiguration<MatchOfficials>
{
    public void Configure(EntityTypeBuilder<MatchOfficials> b)
    {
        b.ToTable("match_officials");
        b.HasKey(x => x.Id);
        b.Property(x => x.AssignmentSource).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ManualReason).HasMaxLength(500);
        b.HasIndex(x => x.MatchId).IsUnique();
    }
}

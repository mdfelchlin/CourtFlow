using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public class MatchConfig : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> b)
    {
        b.ToTable("matches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Wave).HasConversion<int>();
        b.Property(x => x.State).HasConversion<int>();
        b.Property(x => x.StageId).HasColumnName("stage_id");
        b.HasMany(x => x.Sets).WithOne().HasForeignKey(s => s.MatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TournamentId);
        b.HasIndex(x => x.DivisionId);
        b.HasIndex(x => x.PoolId);
        b.HasIndex(x => x.CourtId);
        b.HasIndex(x => x.BracketNodeId);
        b.HasIndex(x => x.StageId);
        b.HasIndex(x => new { x.TournamentId, x.ScheduledStartUtc });
    }
}

public class MatchSetConfig : IEntityTypeConfiguration<MatchSet>
{
    public void Configure(EntityTypeBuilder<MatchSet> b)
    {
        b.ToTable("match_sets");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.MatchId, x.SetNumber }).IsUnique();
    }
}

public class SchedulingTimeSlotConfig : IEntityTypeConfiguration<SchedulingTimeSlot>
{
    public void Configure(EntityTypeBuilder<SchedulingTimeSlot> b)
    {
        b.ToTable("scheduling_time_slots");
        b.HasKey(x => x.Id);
        b.Property(x => x.Wave).HasConversion<int>();
        b.HasIndex(x => new { x.TournamentId, x.CourtId, x.StartUtc });
        b.HasIndex(x => new { x.TournamentId, x.Wave, x.StartUtc });
    }
}

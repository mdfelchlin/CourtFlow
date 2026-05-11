using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public sealed class ScheduleVersionConfig : IEntityTypeConfiguration<ScheduleVersion>
{
    public void Configure(EntityTypeBuilder<ScheduleVersion> b)
    {
        b.ToTable("schedule_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        b.Property(x => x.MutationSequence).IsRequired();
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.HasIndex(x => x.TournamentId);
    }
}

public sealed class MatchAssignmentConfig : IEntityTypeConfiguration<MatchAssignment>
{
    public void Configure(EntityTypeBuilder<MatchAssignment> b)
    {
        b.ToTable("match_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.AssignmentStatus).HasMaxLength(32);
        b.HasIndex(x => x.ScheduleVersionId);
        b.HasIndex(x => x.MatchId);
        b.HasIndex(x => new { x.CourtId, x.ScheduledStart });
    }
}

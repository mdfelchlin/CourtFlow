using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public sealed class MatchSlotSourceConfig : IEntityTypeConfiguration<MatchSlotSource>
{
    public void Configure(EntityTypeBuilder<MatchSlotSource> b)
    {
        b.ToTable("match_slot_sources");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slot).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.SourceKind).HasConversion<string>().HasMaxLength(32);
        b.HasIndex(x => new { x.MatchId, x.Slot }).IsUnique();
        b.HasIndex(x => x.SourceMatchId);
    }
}

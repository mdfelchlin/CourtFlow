using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public sealed class PoolSettingsConfig : IEntityTypeConfiguration<PoolSettings>
{
    public void Configure(EntityTypeBuilder<PoolSettings> b)
    {
        b.ToTable("pool_settings");
        b.HasKey(x => x.StageId);
        b.Property(x => x.PoolSizeStrategy).HasMaxLength(32).IsRequired();
        b.Property(x => x.SeedingMethod).HasMaxLength(32).IsRequired();
        b.Property(x => x.ClubSeparationMode).HasMaxLength(32).IsRequired();
        b.Property(x => x.PlayStyle).HasMaxLength(32).IsRequired();
        b.Property(x => x.CourtAllocationMode).HasMaxLength(32).IsRequired();
        b.Property(x => x.RefAssignmentMode).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.OrganizationId);
    }
}

using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public sealed class TournamentTemplateConfig : IEntityTypeConfiguration<TournamentTemplate>
{
    public void Configure(EntityTypeBuilder<TournamentTemplate> b)
    {
        b.ToTable("tournament_templates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.StructureJson);
        b.HasIndex(x => x.OrganizationId);
    }
}

public sealed class WaveEntityConfig : IEntityTypeConfiguration<WaveEntity>
{
    public void Configure(EntityTypeBuilder<WaveEntity> b)
    {
        b.ToTable("waves");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.DefaultStartTime).HasMaxLength(10);
        b.Property(x => x.DefaultEndTime).HasMaxLength(10);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => new { x.TournamentId, x.Code }).IsUnique();
    }
}

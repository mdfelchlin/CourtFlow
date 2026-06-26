using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public class TournamentConfig : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> b)
    {
        b.ToTable("tournaments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.WebsiteUrl).HasMaxLength(500);
        b.Property(x => x.ContactEmail).HasMaxLength(254);
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.Property(x => x.SessionMode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue(TournamentSessionMode.SingleSession);
        b.Property(x => x.DefaultStartTime).HasMaxLength(10);
        b.Property(x => x.PublicationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue(TournamentPublicationStatus.Draft);
        b.Property(x => x.PublishedAtUtc);
        b.Property(x => x.ArchivedAtUtc);
        b.HasIndex(x => new { x.OrganizationId, x.Name });
        b.HasMany(x => x.Venues).WithOne().HasForeignKey(v => v.TournamentId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Divisions).WithOne().HasForeignKey(d => d.TournamentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VenueConfig : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> b)
    {
        b.ToTable("venues");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Address).HasMaxLength(500);
        b.HasMany(x => x.Courts).WithOne().HasForeignKey(c => c.VenueId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TournamentId);
    }
}

public class CourtConfig : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> b)
    {
        b.ToTable("courts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Tags).HasColumnType("text[]");
        b.HasIndex(x => x.VenueId);
    }
}

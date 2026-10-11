using CourtFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure;

public sealed class CourtFlowDbContext(DbContextOptions<CourtFlowDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Division> Divisions => Set<Division>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Id).ValueGeneratedNever();
            entity.Property(organization => organization.Name).IsRequired();
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.ToTable("tournaments");
            entity.HasKey(tournament => tournament.Id);
            entity.Property(tournament => tournament.Id).ValueGeneratedNever();
            entity.Property(tournament => tournament.Name).IsRequired();
            entity.Property(tournament => tournament.Description).IsRequired();
            entity.Property(tournament => tournament.TimeZoneId).IsRequired();
            entity.HasQueryFilter(tournament => tournament.OrganizationId == TestOrganizationSeed.Id);
            entity.HasMany(tournament => tournament.Venues)
                .WithOne()
                .HasForeignKey(venue => venue.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(tournament => tournament.Venues)
                .HasField("_venues")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(tournament => tournament.Divisions)
                .WithOne()
                .HasForeignKey(division => division.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(tournament => tournament.Divisions)
                .HasField("_divisions")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("venues");
            entity.HasKey(venue => venue.Id);
            entity.Property(venue => venue.Id).ValueGeneratedNever();
            entity.Property(venue => venue.Name).IsRequired();
            entity.Property(venue => venue.Address).IsRequired();
            entity.HasQueryFilter(venue => venue.OrganizationId == TestOrganizationSeed.Id);
            entity.HasMany(venue => venue.Courts)
                .WithOne()
                .HasForeignKey(court => court.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(venue => venue.Courts)
                .HasField("_courts")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Division>(entity =>
        {
            entity.ToTable("divisions");
            entity.HasKey(division => division.Id);
            entity.Property(division => division.Id).ValueGeneratedNever();
            entity.Property(division => division.Name).IsRequired();
            entity.Property(division => division.Gender).HasConversion<string>().IsRequired();
            entity.HasQueryFilter(division => division.OrganizationId == TestOrganizationSeed.Id);
        });

        modelBuilder.Entity<Court>(entity =>
        {
            entity.ToTable("courts");
            entity.HasKey(court => court.Id);
            entity.Property(court => court.Id).ValueGeneratedNever();
            entity.HasQueryFilter(court => court.OrganizationId == TestOrganizationSeed.Id);
            entity.Ignore(court => court.Name);
        });
    }
}

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
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();

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
            entity.HasMany(tournament => tournament.Entries)
                .WithOne()
                .HasForeignKey(entry => entry.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(tournament => tournament.Entries)
                .HasField("_entries")
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

        modelBuilder.Entity<Club>(entity =>
        {
            entity.ToTable("clubs");
            entity.HasKey(club => club.Id);
            entity.Property(club => club.Id).ValueGeneratedNever();
            entity.Property(club => club.Name).IsRequired();
            entity.Property(club => club.NormalizedName).IsRequired();
            entity.HasIndex(club => new { club.OrganizationId, club.NormalizedName }).IsUnique();
            entity.HasQueryFilter(club => club.OrganizationId == TestOrganizationSeed.Id);
            entity.HasMany(club => club.Teams)
                .WithOne()
                .HasForeignKey(team => team.ClubId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(club => club.Teams)
                .HasField("_teams")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(team => team.Id);
            entity.Property(team => team.Id).ValueGeneratedNever();
            entity.Property(team => team.Name).IsRequired();
            entity.Property(team => team.NormalizedName).IsRequired();
            entity.HasIndex(team => new { team.ClubId, team.NormalizedName }).IsUnique();
            entity.HasQueryFilter(team => team.OrganizationId == TestOrganizationSeed.Id);
        });

        modelBuilder.Entity<TournamentTeam>(entity =>
        {
            entity.ToTable("tournament_teams");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).ValueGeneratedNever();
            entity.HasIndex(entry => new { entry.TournamentId, entry.TeamId }).IsUnique();
            entity.HasQueryFilter(entry => entry.OrganizationId == TestOrganizationSeed.Id);
            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(entry => entry.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Division>()
                .WithMany()
                .HasForeignKey(entry => entry.DivisionId)
                .OnDelete(DeleteBehavior.ClientCascade);
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

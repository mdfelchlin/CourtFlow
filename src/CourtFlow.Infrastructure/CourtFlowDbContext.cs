using CourtFlow.Domain;
using CourtFlow.Infrastructure.Access;
using CourtFlow.Infrastructure.Email;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure;

/// <param name="filter">
/// The request's organization and viewer flag. Without one, as at design time, the organization filters match nothing.
/// </param>
public sealed class CourtFlowDbContext(DbContextOptions<CourtFlowDbContext> options, DataFilter? filter = null)
    : DbContext(options), IDataProtectionKeyContext
{
    // Read at query time, so one compiled model serves every request's organization.
    private Guid CurrentOrganizationId => filter?.OrganizationId ?? Guid.Empty;
    private bool PublishedOnly => filter?.PublishedOnly ?? false;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
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
            entity.Property(organization => organization.Status).HasConversion<string>().IsRequired();
            entity.Ignore(organization => organization.IsActive);
            entity.Ignore(organization => organization.IsVisibleToPublic);
            entity.HasMany(organization => organization.Memberships)
                .WithOne()
                .HasForeignKey(membership => membership.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(organization => organization.Memberships)
                .HasField("_memberships")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.Id).ValueGeneratedNever();
            entity.Property(membership => membership.UserId).IsRequired();
            entity.Property(membership => membership.Email).IsRequired();
            entity.Property(membership => membership.Name).IsRequired();
            entity.Property(membership => membership.Role).HasConversion<string>().IsRequired();
            entity.HasIndex(membership => membership.UserId);
            entity.HasIndex(membership => membership.Email);
            entity.HasIndex(membership => new { membership.OrganizationId, membership.UserId }).IsUnique();
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations");
            entity.HasKey(invitation => invitation.Id);
            entity.Property(invitation => invitation.Id).ValueGeneratedNever();
            entity.Property(invitation => invitation.Email).IsRequired();
            entity.Property(invitation => invitation.Role).HasConversion<string>().IsRequired();
            entity.Property(invitation => invitation.State).HasConversion<string>().IsRequired();
            entity.Property(invitation => invitation.WorkOsInvitationId).IsRequired();
            entity.HasIndex(invitation => invitation.Email);
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(invitation => invitation.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.ToTable("access_requests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Id).ValueGeneratedNever();
            entity.Property(request => request.WaitlistEntryId).IsRequired();
            entity.Property(request => request.Name).IsRequired();
            entity.Property(request => request.Email).IsRequired();
            entity.Property(request => request.OrganizationName).IsRequired();
            entity.Property(request => request.Message).IsRequired();
            entity.Property(request => request.State).HasConversion<string>().IsRequired();
            entity.HasIndex(request => request.Email);
        });

        modelBuilder.Entity<OutboxEmail>(entity =>
        {
            entity.ToTable("outbox_emails");
            entity.HasKey(email => email.Id);
            entity.Property(email => email.Id).ValueGeneratedNever();
            entity.Property(email => email.To).IsRequired();
            entity.Property(email => email.Subject).IsRequired();
            entity.Property(email => email.Text).IsRequired();
            entity.Property(email => email.Html).IsRequired();
            entity.HasIndex(email => email.NextAttemptAt);
        });

        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.ToTable("tournaments");
            entity.HasKey(tournament => tournament.Id);
            entity.Property(tournament => tournament.Id).ValueGeneratedNever();
            entity.Property(tournament => tournament.Name).IsRequired();
            entity.Property(tournament => tournament.Description).IsRequired();
            entity.Property(tournament => tournament.TimeZoneId).IsRequired();
            entity.Property(tournament => tournament.Status).HasConversion<string>().IsRequired();
            entity.Property(tournament => tournament.LastPublishedAt);
            // A viewer's unpublished and archived tournaments are simply not found, and nor are their children,
            // because every venue, division, and team use case loads its tournament first.
            entity.HasQueryFilter(tournament =>
                tournament.OrganizationId == CurrentOrganizationId
                && (!PublishedOnly || tournament.Status == TournamentStatus.Published));
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
            entity.HasMany(tournament => tournament.Rankings)
                .WithOne()
                .HasForeignKey(place => place.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(tournament => tournament.Rankings)
                .HasField("_rankings")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("venues");
            entity.HasKey(venue => venue.Id);
            entity.Property(venue => venue.Id).ValueGeneratedNever();
            entity.Property(venue => venue.Name).IsRequired();
            entity.Property(venue => venue.Address).IsRequired();
            entity.HasQueryFilter(venue => venue.OrganizationId == CurrentOrganizationId);
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
            entity.HasQueryFilter(division => division.OrganizationId == CurrentOrganizationId);
        });

        modelBuilder.Entity<Club>(entity =>
        {
            entity.ToTable("clubs");
            entity.HasKey(club => club.Id);
            entity.Property(club => club.Id).ValueGeneratedNever();
            entity.Property(club => club.Name).IsRequired();
            entity.Property(club => club.NormalizedName).IsRequired();
            entity.HasIndex(club => new { club.OrganizationId, club.NormalizedName }).IsUnique();
            entity.HasQueryFilter(club => club.OrganizationId == CurrentOrganizationId);
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
            entity.HasQueryFilter(team => team.OrganizationId == CurrentOrganizationId);
        });

        modelBuilder.Entity<TournamentTeam>(entity =>
        {
            entity.ToTable("tournament_teams");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).ValueGeneratedNever();
            entity.HasIndex(entry => new { entry.TournamentId, entry.TeamId }).IsUnique();
            entity.HasQueryFilter(entry => entry.OrganizationId == CurrentOrganizationId);
            entity.HasOne<Team>()
                .WithMany()
                .HasForeignKey(entry => entry.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Division>()
                .WithMany()
                .HasForeignKey(entry => entry.DivisionId)
                .OnDelete(DeleteBehavior.ClientCascade);
        });

        modelBuilder.Entity<RankingPlace>(entity =>
        {
            entity.ToTable("ranking_places");
            entity.HasKey(place => place.Id);
            entity.Property(place => place.Id).ValueGeneratedNever();
            entity.HasIndex(place => place.EntryId).IsUnique();
            entity.HasIndex(place => new { place.DivisionId, place.Rank }).IsUnique();
            entity.HasQueryFilter(place => place.OrganizationId == CurrentOrganizationId);
            entity.HasOne<Division>()
                .WithMany()
                .HasForeignKey(place => place.DivisionId)
                .OnDelete(DeleteBehavior.ClientCascade);
            entity.HasOne<TournamentTeam>()
                .WithMany()
                .HasForeignKey(place => place.EntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Court>(entity =>
        {
            entity.ToTable("courts");
            entity.HasKey(court => court.Id);
            entity.Property(court => court.Id).ValueGeneratedNever();
            entity.HasQueryFilter(court => court.OrganizationId == CurrentOrganizationId);
            entity.Ignore(court => court.Name);
        });
    }
}

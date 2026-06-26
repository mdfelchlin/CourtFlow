using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Common;
using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Pool> Pools => Set<Pool>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<Bracket> Brackets => Set<Bracket>();
    public DbSet<BracketNode> BracketNodes => Set<BracketNode>();
    public DbSet<SchedulingTimeSlot> SchedulingTimeSlots => Set<SchedulingTimeSlot>();
    public DbSet<ScheduleVersion> ScheduleVersions => Set<ScheduleVersion>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<DivisionRanking> DivisionRankings => Set<DivisionRanking>();
    public DbSet<PoolEntry> PoolEntries => Set<PoolEntry>();
    public DbSet<AdvancementRule> AdvancementRules => Set<AdvancementRule>();
    public DbSet<BracketSeedSlot> BracketSeedSlots => Set<BracketSeedSlot>();
    public DbSet<PoolCourtAllocation> PoolCourtAllocations => Set<PoolCourtAllocation>();
    public DbSet<PoolSettings> PoolSettings => Set<PoolSettings>();
    public DbSet<BracketSeedSlotOverride> BracketSeedSlotOverrides => Set<BracketSeedSlotOverride>();
    public DbSet<MatchAssignment> MatchAssignments => Set<MatchAssignment>();
    public DbSet<MatchOfficials> MatchOfficials => Set<MatchOfficials>();
    public DbSet<RepairJob> RepairJobs => Set<RepairJob>();
    public DbSet<RepairAction> RepairActions => Set<RepairAction>();
    public DbSet<CourtOutage> CourtOutages => Set<CourtOutage>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<TournamentTemplate> TournamentTemplates => Set<TournamentTemplate>();
    public DbSet<WaveEntity> Waves => Set<WaveEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                method.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => _tenant.OrganizationId == null || e.OrganizationId == _tenant.OrganizationId);
    }
}

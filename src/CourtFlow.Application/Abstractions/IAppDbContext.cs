using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationMembership> OrganizationMemberships { get; }
    DbSet<Tournament> Tournaments { get; }
    DbSet<Venue> Venues { get; }
    DbSet<Court> Courts { get; }
    DbSet<Division> Divisions { get; }
    DbSet<Pool> Pools { get; }
    DbSet<Team> Teams { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchSet> MatchSets { get; }
    DbSet<Bracket> Brackets { get; }
    DbSet<BracketNode> BracketNodes { get; }
    DbSet<SchedulingTimeSlot> SchedulingTimeSlots { get; }
    DbSet<ScheduleVersion> ScheduleVersions { get; }
    DbSet<Stage> Stages { get; }
    DbSet<TournamentTeam> TournamentTeams { get; }
    DbSet<DivisionRanking> DivisionRankings { get; }
    DbSet<PoolEntry> PoolEntries { get; }
    DbSet<AdvancementRule> AdvancementRules { get; }
    DbSet<BracketSeedSlot> BracketSeedSlots { get; }
    DbSet<PoolCourtAllocation> PoolCourtAllocations { get; }
    DbSet<BracketSeedSlotOverride> BracketSeedSlotOverrides { get; }
    DbSet<MatchAssignment> MatchAssignments { get; }
    DbSet<MatchOfficials> MatchOfficials { get; }
    DbSet<RepairJob> RepairJobs { get; }
    DbSet<RepairAction> RepairActions { get; }
    DbSet<CourtOutage> CourtOutages { get; }
    DbSet<ImportJob> ImportJobs { get; }
    DbSet<ImportRow> ImportRows { get; }
    DbSet<TournamentTemplate> TournamentTemplates { get; }
    DbSet<WaveEntity> Waves { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

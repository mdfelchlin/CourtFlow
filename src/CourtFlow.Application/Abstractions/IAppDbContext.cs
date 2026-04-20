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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

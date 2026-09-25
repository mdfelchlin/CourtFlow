using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Observability;
using CourtFlow.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;

namespace CourtFlow.Application.Tests;

public class ScheduleGenerationTests
{
    [Fact]
    public async Task EnsureCurrentScheduleVersion_CreatesDraftVersion()
    {
        await using var db = ImportFeaturesTests.BuildDb(); // shared in-memory fixture
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        var version = await ScheduleVersionProvisioning.EnsureCurrentAsync(db, tournament.Id);

        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal(tournament.Id, version.TournamentId);
        Assert.Equal(1, await db.ScheduleVersions.CountAsync(v => v.TournamentId == tournament.Id));
    }

    [Fact]
    public async Task PoolGeneratePattern_WritesMatchAssignmentsWithoutMatchCourt()
    {
        await using var db = ImportFeaturesTests.BuildDb(); // shared in-memory fixture
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var pool = new Pool(orgId, division.Id, "Pool A");
        var venue = new Venue(orgId, tournament.Id, "Main");
        var court = new Court(orgId, venue.Id, "Court 1", Array.Empty<string>());
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Pools.Add(pool);
        db.Venues.Add(venue);
        db.Courts.Add(court);
        var t1 = new Team(orgId, division.Id, "A", Wave.AM, pool.Id, null, 1);
        var t2 = new Team(orgId, division.Id, "B", Wave.AM, pool.Id, null, 2);
        db.Teams.AddRange(t1, t2);
        await db.SaveChangesAsync();
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);

        var version = await ScheduleVersionProvisioning.EnsureCurrentAsync(db, tournament.Id);
        var stage = await db.Stages.FirstAsync(s => s.DivisionId == division.Id && s.StageType == StageType.Pool);

        var day = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var match = new Match(orgId, tournament.Id, division.Id, Wave.AM, day, 60, pool.Id, null, null, t1.Id, t2.Id, null, stage.Id);
        db.Matches.Add(match);
        db.MatchAssignments.Add(new MatchAssignment(
            orgId, version.Id, match.Id, court.Id, day.AddHours(8), day.AddHours(9)));
        await db.SaveChangesAsync();

        var saved = await db.Matches.FirstAsync(m => m.Id == match.Id);
        Assert.Null(saved.CourtId);
        Assert.Equal(day, saved.ScheduledStartUtc);

        var assignments = await db.MatchAssignments.Where(a => a.ScheduleVersionId == version.Id).ToListAsync();
        Assert.Single(assignments);
        Assert.Equal(court.Id, assignments[0].CourtId);
    }

    [Fact]
    public async Task GeneratePoolPlay_NoDuplicateCourtSlotOnSameVersion()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 5, 12), new DateOnly(2026, 5, 14), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var poolA = new Pool(orgId, division.Id, "Pool A");
        var poolB = new Pool(orgId, division.Id, "Pool B");
        var venue = new Venue(orgId, tournament.Id, "Main");
        var court = new Court(orgId, venue.Id, "Court 1", Array.Empty<string>());
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Pools.AddRange(poolA, poolB);
        db.Venues.Add(venue);
        db.Courts.Add(court);
        db.Teams.AddRange(
            new Team(orgId, division.Id, "A1", Wave.AM, poolA.Id, null, 1),
            new Team(orgId, division.Id, "A2", Wave.AM, poolA.Id, null, 2),
            new Team(orgId, division.Id, "B1", Wave.AM, poolB.Id, null, 3),
            new Team(orgId, division.Id, "B2", Wave.AM, poolB.Id, null, 4));
        await db.SaveChangesAsync();
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);

        var generator = new RoundRobinScheduleGenerator(db, new FakeTenant(orgId), TestMetrics.Create());
        await generator.GeneratePoolPlayAsync(tournament.Id);
        await generator.GeneratePoolPlayAsync(tournament.Id);

        var version = await db.ScheduleVersions
            .Where(v => v.TournamentId == tournament.Id)
            .OrderByDescending(v => v.MutationSequence)
            .FirstAsync();
        var assignments = await db.MatchAssignments
            .Where(a => a.ScheduleVersionId == version.Id)
            .ToListAsync();

        var courtSlots = assignments.Select(a => (a.CourtId, a.ScheduledStart)).ToList();
        Assert.Equal(courtSlots.Count, courtSlots.Distinct().Count());
    }

    private sealed class FakeTenant(Guid orgId) : ITenantContext
    {
        public Guid? OrganizationId => orgId;
        public string? UserId => "test";
        public bool IsAuthenticated => true;
        public Guid RequireOrganizationId() => orgId;
        public string RequireUserId() => "test";
    }
}

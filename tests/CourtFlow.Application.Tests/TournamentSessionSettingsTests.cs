using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.Features.Tournaments;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Observability;
using CourtFlow.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Tests;

public class TournamentSessionSettingsTests
{
    [Fact]
    public async Task Upsert_MultiWave_CreatesAmAndPmWavesWithStartTimes()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), "America/Los_Angeles");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        var handler = new UpsertTournamentSessionSettingsHandler(db, new FakeTenant(orgId));
        var result = await handler.Handle(
            new UpsertTournamentSessionSettingsCommand(
                tournament.Id,
                TournamentSessionMode.MultiWave,
                null,
                "09:30",
                "14:15"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(TournamentSessionMode.MultiWave, result!.SessionMode);
        Assert.Null(result.DefaultStartTime);

        var waves = await db.Waves.Where(w => w.TournamentId == tournament.Id).OrderBy(w => w.Code).ToListAsync();
        Assert.Equal(2, waves.Count);
        Assert.Equal("09:30", waves.First(w => w.Code == "AM").DefaultStartTime);
        Assert.Equal("14:15", waves.First(w => w.Code == "PM").DefaultStartTime);
        Assert.Null(waves[0].DefaultEndTime);
    }

    [Fact]
    public async Task Upsert_SingleSession_ClearsDivisionWavePins()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), "America/Los_Angeles");
        tournament.SetSessionMode(TournamentSessionMode.MultiWave);
        var wave = new WaveEntity(orgId, tournament.Id, "AM", 0);
        wave.SetTimeWindow("08:00", null);
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 16);
        division.SetWave(wave.Id);
        db.Tournaments.Add(tournament);
        db.Waves.Add(wave);
        db.Divisions.Add(division);
        await db.SaveChangesAsync();

        var handler = new UpsertTournamentSessionSettingsHandler(db, new FakeTenant(orgId));
        await handler.Handle(
            new UpsertTournamentSessionSettingsCommand(
                tournament.Id,
                TournamentSessionMode.SingleSession,
                "10:00",
                null,
                null),
            CancellationToken.None);

        var savedDivision = await db.Divisions.FirstAsync(d => d.Id == division.Id);
        Assert.Null(savedDivision.WaveId);

        var savedTournament = await db.Tournaments.FirstAsync(t => t.Id == tournament.Id);
        Assert.Equal(TournamentSessionMode.SingleSession, savedTournament.SessionMode);
        Assert.Equal("10:00", savedTournament.DefaultStartTime);
    }

    [Fact]
    public async Task GeneratePoolPlay_SingleSession_UsesTournamentStartTime()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), "America/Los_Angeles");
        tournament.SetSessionMode(TournamentSessionMode.SingleSession);
        tournament.SetDefaultStartTime("10:30");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var pool = new Pool(orgId, division.Id, "Pool A");
        var venue = new Venue(orgId, tournament.Id, "Main");
        var court = new Court(orgId, venue.Id, "Court 1", Array.Empty<string>());
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Pools.Add(pool);
        db.Venues.Add(venue);
        db.Courts.Add(court);
        db.Teams.AddRange(
            new Team(orgId, division.Id, "A", Wave.PM, pool.Id, null, 1),
            new Team(orgId, division.Id, "B", Wave.PM, pool.Id, null, 2));
        await db.SaveChangesAsync();
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);

        var generator = new RoundRobinScheduleGenerator(db, new FakeTenant(orgId), TestMetrics.Create());
        await generator.GeneratePoolPlayAsync(tournament.Id);

        var version = await db.ScheduleVersions.FirstAsync(v => v.TournamentId == tournament.Id);
        var assignment = await db.MatchAssignments.FirstAsync(a => a.ScheduleVersionId == version.Id);
        var day = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        Assert.Equal(day.AddHours(10).AddMinutes(30), assignment.ScheduledStart);
    }

    [Fact]
    public async Task GeneratePoolPlay_MultiWave_UsesWaveStartTimes()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), "America/Los_Angeles");
        tournament.SetSessionMode(TournamentSessionMode.MultiWave);
        var amWave = new WaveEntity(orgId, tournament.Id, "AM", 0);
        amWave.SetTimeWindow("07:15", null);
        var pmWave = new WaveEntity(orgId, tournament.Id, "PM", 1);
        pmWave.SetTimeWindow("15:45", null);
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var pool = new Pool(orgId, division.Id, "Pool A");
        var venue = new Venue(orgId, tournament.Id, "Main");
        var court = new Court(orgId, venue.Id, "Court 1", Array.Empty<string>());
        db.Tournaments.Add(tournament);
        db.Waves.AddRange(amWave, pmWave);
        db.Divisions.Add(division);
        db.Pools.Add(pool);
        db.Venues.Add(venue);
        db.Courts.Add(court);
        db.Teams.AddRange(
            new Team(orgId, division.Id, "A", Wave.PM, pool.Id, null, 1),
            new Team(orgId, division.Id, "B", Wave.PM, pool.Id, null, 2));
        await db.SaveChangesAsync();
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);

        var generator = new RoundRobinScheduleGenerator(db, new FakeTenant(orgId), TestMetrics.Create());
        await generator.GeneratePoolPlayAsync(tournament.Id);

        var version = await db.ScheduleVersions.FirstAsync(v => v.TournamentId == tournament.Id);
        var assignment = await db.MatchAssignments.FirstAsync(a => a.ScheduleVersionId == version.Id);
        var day = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        Assert.Equal(day.AddHours(15).AddMinutes(45), assignment.ScheduledStart);
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

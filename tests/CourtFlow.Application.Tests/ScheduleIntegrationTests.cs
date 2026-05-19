using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.ReadModels.DivisionReadiness;
using CourtFlow.Application.ReadModels.MatchClockDelaySignal;
using CourtFlow.Application.ReadModels.PublicEventReadiness;
using CourtFlow.Application.ReadModels.TiebreakerChain;
using CourtFlow.Application.Reflow;
using CourtFlow.Application.Scheduling;
using CourtFlow.Application.StageMaterialization.Commands;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Observability;
using CourtFlow.Infrastructure.Reflow;
using CourtFlow.Infrastructure.Scheduling;
using CourtFlow.Infrastructure.StageMaterialization;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;
using CourtFlow.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CourtFlow.Application.Tests;

public class ScheduleIntegrationTests
{
    [Fact]
    public async Task PublishScheduleVersion_MakesPublicScheduleReady()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        tournament.Publish();
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

        var generator = new RoundRobinScheduleGenerator(db, new FakeTenant(orgId), TestMetrics.Create());
        await generator.GeneratePoolPlayAsync(tournament.Id);

        var publishHandler = new PublishScheduleVersionHandler(db);
        var published = await publishHandler.Handle(new PublishScheduleVersionCommand(tournament.Id), CancellationToken.None);
        Assert.NotNull(published);
        Assert.Equal(ScheduleVersionStatus.Published, published!.Status);

        var readiness = new PublicEventReadinessService(db);
        var gates = await readiness.EvaluateAsync(tournament.Id);
        Assert.True(gates.ScheduleReady);
    }

    [Fact]
    public async Task ForkOnMutation_WhenPublishedVersionExists()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        var v1 = await ScheduleVersionProvisioning.EnsureCurrentAsync(db, tournament.Id);
        await ScheduleVersionProvisioning.PublishAsync(db, tournament.Id);

        var v2 = await ScheduleVersionProvisioning.EnsureWorkingForMutationAsync(db, tournament.Id);
        Assert.NotEqual(v1.Id, v2.Id);
        Assert.Equal(ScheduleVersionStatus.Draft, v2.Status);

        var versions = await db.ScheduleVersions.Where(v => v.TournamentId == tournament.Id).ToListAsync();
        Assert.Equal(ScheduleVersionStatus.Superseded, versions.Single(v => v.Id == v1.Id).Status);
    }

    [Fact]
    public void MatchClockDelaySignal_ReturnsNullForNotStarted()
    {
        var delay = MatchClockDelaySignal.ComputeDelayMinutes(
            MatchState.NotStarted,
            DateTime.UtcNow.AddHours(1),
            null,
            null,
            DateTime.UtcNow);
        Assert.Null(delay);
    }

    [Fact]
    public async Task StartMatch_SetsInProgressAndActualStart()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await db.SaveChangesAsync();

        var match = new Match(orgId, tournament.Id, division.Id, Wave.AM, DateTime.UtcNow, 60);
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        var handler = new Features.Matches.StartMatchHandler(db);
        var dto = await handler.Handle(new Features.Matches.StartMatchCommand(match.Id), CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal(MatchState.InProgress, dto!.State);
        Assert.NotNull(dto.ActualStartUtc);
    }

    [Fact]
    public async Task PreviewMove_WithCourtConflict_CannotApply()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var (tournament, court, m1, m2) = await SeedTwoCourtMatchesAsync(db, orgId);
        var checker = new TestConstraintChecker(db);
        var issuer = new ScheduleApplyTokenIssuer(Options.Create(new ScheduleVersionsOptions()));
        var handler = new PreviewScheduleMoveHandler(db, checker, issuer);

        var overlapStart = m2.ScheduledStartUtc.AddMinutes(15);
        var preview = await handler.Handle(new PreviewScheduleMoveCommand(
            tournament.Id, m1.Id, court.Id, overlapStart, 60), CancellationToken.None);

        Assert.NotNull(preview);
        Assert.True(preview!.Conflicts.HasConflicts);
        Assert.False(preview.CanApply);
        Assert.Empty(preview.ApplyToken);
    }

    [Fact]
    public async Task PreviewMove_NoConflict_ApplyMoveUpdatesAssignment()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var (tournament, court, m1, _) = await SeedTwoCourtMatchesAsync(db, orgId);
        var checker = new TestConstraintChecker(db);
        var issuer = new ScheduleApplyTokenIssuer(Options.Create(new ScheduleVersionsOptions()));
        var previewHandler = new PreviewScheduleMoveHandler(db, checker, issuer);
        var applyHandler = new ApplyScheduleMoveHandler(db, checker, issuer);

        var newStart = m1.ScheduledStartUtc.AddHours(3);
        var preview = await previewHandler.Handle(new PreviewScheduleMoveCommand(
            tournament.Id, m1.Id, court.Id, newStart, 60), CancellationToken.None);

        Assert.NotNull(preview);
        Assert.False(preview!.Conflicts.HasConflicts);
        Assert.True(preview.CanApply);
        Assert.NotEmpty(preview.ApplyToken);

        var applied = await applyHandler.Handle(new ApplyScheduleMoveCommand(tournament.Id, preview.ApplyToken), CancellationToken.None);
        Assert.NotNull(applied);
        Assert.Equal(newStart, applied!.ScheduledStartUtc);

        var version = await db.ScheduleVersions.FirstAsync(v => v.TournamentId == tournament.Id);
        var assignment = await db.MatchAssignments.FirstAsync(a => a.MatchId == m1.Id && a.ScheduleVersionId == version.Id);
        Assert.Equal(newStart, assignment.ScheduledStart);
    }

    [Fact]
    public async Task ReflowApply_CreatesRepairJobAndShiftsAssignments()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var venue = new Venue(orgId, tournament.Id, "Main");
        var court = new Court(orgId, venue.Id, "Court 1", Array.Empty<string>());
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Venues.Add(venue);
        db.Courts.Add(court);
        await db.SaveChangesAsync();

        var version = await ScheduleVersionProvisioning.EnsureCurrentAsync(db, tournament.Id);
        var start = DateTime.UtcNow.AddHours(-2);
        var delayed = new Match(orgId, tournament.Id, division.Id, Wave.AM, start, 60, courtId: court.Id);
        delayed.Start();
        var follow = new Match(orgId, tournament.Id, division.Id, Wave.AM, start.AddMinutes(70), 60, courtId: court.Id);
        db.Matches.AddRange(delayed, follow);
        await db.SaveChangesAsync();

        db.MatchAssignments.Add(new MatchAssignment(orgId, version.Id, delayed.Id, court.Id, start, start.AddMinutes(60)));
        db.MatchAssignments.Add(new MatchAssignment(orgId, version.Id, follow.Id, court.Id, start.AddMinutes(70), start.AddMinutes(130)));
        await db.SaveChangesAsync();

        var engine = new ReflowPreviewEngine(db, new ReflowApplyTokenIssuer(Options.Create(new ScheduleVersionsOptions())));
        var preview = await engine.PreviewAsync(tournament.Id);
        Assert.NotNull(preview.ApplyToken);
        Assert.NotEmpty(preview.Moves);

        var result = await engine.ApplyAsync(tournament.Id, preview.ApplyToken!, "test-user", CancellationToken.None);
        Assert.True(result.AssignmentsUpdated > 0);

        var job = await db.RepairJobs.Include(j => j.Actions).FirstAsync(j => j.Id == result.RepairJobId);
        Assert.Equal(RepairTriggerType.ScheduleReflow, job.TriggerType);
        Assert.Equal(RepairJobStatus.Applied, job.Status);
        Assert.NotEmpty(job.Actions);
    }

    [Fact]
    public async Task DivisionReadiness_LocksRankingsWhenStageInProgress()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await db.SaveChangesAsync();
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        var poolStage = await db.Stages.FirstAsync(s => s.DivisionId == division.Id && s.StageType == StageType.Pool);
        poolStage.TransitionTo(StageStatus.Configured);
        poolStage.TransitionTo(StageStatus.Seeded);
        poolStage.TransitionTo(StageStatus.InProgress);
        await db.SaveChangesAsync();

        var readiness = new DivisionReadinessService(db);
        var gates = await readiness.EvaluateAsync(division.Id);
        Assert.False(gates.CanEditRankings);
        Assert.Contains(gates.BlockingReasons, r => r.Contains("Rankings are locked", StringComparison.Ordinal));
    }

    [Fact]
    public void TiebreakerChain_OrdersByWinsThenSetPercentage()
    {
        var chain = new TiebreakerChainService();
        var teams = new[] { (Id: Guid.NewGuid(), Name: "A"), (Id: Guid.NewGuid(), Name: "B") };
        var matches = new List<Match>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Wave.AM, DateTime.UtcNow, 60,
                homeTeamId: teams[0].Id, awayTeamId: teams[1].Id)
        };
        matches[0].AssignTeams(teams[0].Id, teams[1].Id);
        matches[0].ReportSet(1, 25, 20);
        matches[0].ReportSet(2, 25, 22);
        matches[0].Finalize(teams[0].Id);

        var sets = matches[0].Sets.ToList();
        var ranked = chain.OrderTeams(teams, matches, sets);
        Assert.Equal(teams[0].Id, ranked[0].TeamId);
        Assert.Equal(teams[1].Id, ranked[1].TeamId);
        Assert.Equal("match_wins", ranked[0].TiebreakStep);
    }

    [Fact]
    public async Task SnakeIntoPools_AssignsWorkTeams()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var pool = new Pool(orgId, division.Id, "Pool A");
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Pools.Add(pool);
        db.Teams.AddRange(
            new Team(orgId, division.Id, "A", Wave.AM, pool.Id, null, 1),
            new Team(orgId, division.Id, "B", Wave.AM, pool.Id, null, 2),
            new Team(orgId, division.Id, "C", Wave.AM, pool.Id, null, 3));
        await db.SaveChangesAsync();
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        var materializer = new StageMaterializationService(db, new FakeStandingsCalculator());
        await materializer.SnakeIntoPoolsAsync(division.Id);

        var officials = await db.MatchOfficials.ToListAsync();
        Assert.NotEmpty(officials);
        Assert.All(officials, o => Assert.NotNull(o.WorkTeamTournamentTeamId));
    }

    private static async Task<(Tournament tournament, Court court, Match m1, Match m2)> SeedTwoCourtMatchesAsync(
        IAppDbContext db, Guid orgId)
    {
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        var venue = new Venue(orgId, tournament.Id, "Main");
        var court = new Court(orgId, venue.Id, "Court 1", Array.Empty<string>());
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        db.Venues.Add(venue);
        db.Courts.Add(court);
        await db.SaveChangesAsync();

        var version = await ScheduleVersionProvisioning.EnsureCurrentAsync(db, tournament.Id);
        var start = DateTime.UtcNow.Date.AddHours(9);
        var m1 = new Match(orgId, tournament.Id, division.Id, Wave.AM, start, 60, courtId: court.Id);
        var m2 = new Match(orgId, tournament.Id, division.Id, Wave.AM, start.AddMinutes(70), 60, courtId: court.Id);
        db.Matches.AddRange(m1, m2);
        await db.SaveChangesAsync();

        db.MatchAssignments.Add(new MatchAssignment(orgId, version.Id, m1.Id, court.Id, start, start.AddMinutes(60)));
        db.MatchAssignments.Add(new MatchAssignment(orgId, version.Id, m2.Id, court.Id, start.AddMinutes(70), start.AddMinutes(130)));
        await db.SaveChangesAsync();

        return (tournament, court, m1, m2);
    }

    private sealed class TestConstraintChecker(IAppDbContext db) : IConstraintChecker
    {
        public async Task<ConflictReport> CheckTournamentAsync(Guid tournamentId, CancellationToken ct = default)
        {
            var matches = await db.Matches.Where(m => m.TournamentId == tournamentId && m.State != MatchState.Canceled).ToListAsync(ct);
            return new ConflictReport(FindCourtConflicts(matches));
        }

        public async Task<ConflictReport> CheckHypotheticalMoveAsync(
            Guid tournamentId,
            Guid matchId,
            Guid? proposedCourtId,
            DateTime proposedStartUtc,
            int proposedDurationMinutes,
            CancellationToken ct)
        {
            var matches = await db.Matches
                .Where(m => m.TournamentId == tournamentId && m.State != MatchState.Canceled)
                .ToListAsync(ct);

            if (matches.All(m => m.Id != matchId))
                return new ConflictReport(Array.Empty<ScheduleConflict>());

            var end = proposedStartUtc.AddMinutes(proposedDurationMinutes);
            var conflicts = new List<ScheduleConflict>();
            foreach (var other in matches.Where(m => m.Id != matchId && m.CourtId == proposedCourtId && m.CourtId != null))
            {
                var otherEnd = other.ScheduledStartUtc.AddMinutes(other.DurationMinutes);
                if (proposedStartUtc < otherEnd && other.ScheduledStartUtc < end)
                {
                    conflicts.Add(new ScheduleConflict(
                        ConflictKind.CourtDoubleBooking,
                        matchId,
                        other.Id,
                        null,
                        proposedCourtId,
                        proposedStartUtc,
                        end,
                        "Court double-booked"));
                }
            }
            return new ConflictReport(conflicts);
        }

        public Task<ConflictReport> CheckMatchAsync(Guid matchId, CancellationToken ct = default)
            => Task.FromResult(new ConflictReport(Array.Empty<ScheduleConflict>()));

        private static IReadOnlyList<ScheduleConflict> FindCourtConflicts(IReadOnlyList<Match> matches)
        {
            var conflicts = new List<ScheduleConflict>();
            for (var i = 0; i < matches.Count; i++)
            {
                for (var j = i + 1; j < matches.Count; j++)
                {
                    var a = matches[i];
                    var b = matches[j];
                    if (a.CourtId is null || a.CourtId != b.CourtId) continue;
                    var aEnd = a.ScheduledStartUtc.AddMinutes(a.DurationMinutes);
                    var bEnd = b.ScheduledStartUtc.AddMinutes(b.DurationMinutes);
                    if (a.ScheduledStartUtc < bEnd && b.ScheduledStartUtc < aEnd)
                    {
                        conflicts.Add(new ScheduleConflict(
                            ConflictKind.CourtDoubleBooking, a.Id, b.Id, null, a.CourtId, a.ScheduledStartUtc, aEnd, "Court double-booked"));
                    }
                }
            }
            return conflicts;
        }
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

internal static class TestMetrics
{
    public static CourtFlowMetrics Create() =>
        new(new TestMeterFactory());

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options.Name ?? CourtFlowMetrics.MeterName);
        public Meter Create(string name, string? version = null, IEnumerable<KeyValuePair<string, object?>>? tags = null, object? state = null)
            => new(name, version);
        public void Dispose() { }
    }
}

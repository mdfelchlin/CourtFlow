using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Persistence;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Scheduling;

public class RoundRobinScheduleGenerator : IScheduleGenerator
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly CourtFlowMetrics _metrics;

    public RoundRobinScheduleGenerator(IAppDbContext db, ITenantContext tenant, CourtFlowMetrics metrics)
    {
        _db = db;
        _tenant = tenant;
        _metrics = metrics;
    }

    public async Task<IReadOnlyCollection<Match>> GeneratePoolPlayAsync(Guid tournamentId, CancellationToken ct = default)
    {
        _ = _tenant.RequireOrganizationId();

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
            ?? throw new InvalidOperationException("Tournament not found");

        var scheduleVersion = await ScheduleVersionProvisioning.EnsureWorkingForMutationAsync(_db, tournamentId, ct);

        var divisions = await _db.Divisions.Where(d => d.TournamentId == tournamentId).ToListAsync(ct);
        var divisionIdsForStages = divisions.Select(d => d.Id).ToList();
        var poolStages = await _db.Stages
            .Where(s => divisionIdsForStages.Contains(s.DivisionId) && s.StageType == StageType.Pool)
            .ToListAsync(ct);
        var poolStageIdByDivision = poolStages
            .GroupBy(s => s.DivisionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StageNumber).First().Id);
        var pools = await _db.Pools
            .Where(p => divisions.Select(d => d.Id).Contains(p.DivisionId))
            .ToListAsync(ct);
        var teams = await _db.Teams
            .Where(t => divisions.Select(d => d.Id).Contains(t.DivisionId))
            .ToListAsync(ct);

        var existingMatchIds = await _db.Matches
            .Where(m => m.TournamentId == tournamentId && m.PoolId != null && m.BracketNodeId == null)
            .Select(m => m.Id)
            .ToListAsync(ct);
        if (existingMatchIds.Count > 0)
        {
            await EntityGraphDeletes.BulkDeleteQueryAsync(
                _db,
                _db.MatchAssignments.Where(a =>
                    a.ScheduleVersionId == scheduleVersion.Id && existingMatchIds.Contains(a.MatchId)),
                ct);
            await EntityGraphDeletes.BulkDeleteQueryAsync(
                _db,
                _db.Matches.Where(m => existingMatchIds.Contains(m.Id)),
                ct);
        }

        var courts = await _db.Courts
            .Where(c => _db.Venues.Where(v => v.TournamentId == tournamentId).Select(v => v.Id).Contains(c.VenueId))
            .ToListAsync(ct);

        var amCourts = courts.Where(c => !c.Tags.Any(t => t.Equals("PM", StringComparison.OrdinalIgnoreCase))).ToList();
        var pmCourts = courts.Where(c => !c.Tags.Any(t => t.Equals("AM", StringComparison.OrdinalIgnoreCase))).ToList();
        if (amCourts.Count == 0) amCourts = courts;
        if (pmCourts.Count == 0) pmCourts = courts;

        var duration = tournament.DefaultMatchDurationMinutes;
        var day = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var placeholderStart = day;
        var amStart = day.AddHours(8);
        var pmStart = day.AddHours(13);

        var created = new List<Match>();
        var assignments = new List<MatchAssignment>();
        var nextSlotByWave = new Dictionary<Wave, int> { [Wave.AM] = 0, [Wave.PM] = 0 };

        foreach (var pool in pools)
        {
            var poolTeams = teams.Where(t => t.PoolId == pool.Id).ToList();
            if (poolTeams.Count < 2) continue;

            var firstTeam = poolTeams[0];
            var wave = firstTeam.Wave;
            var waveStart = wave == Wave.AM ? amStart : pmStart;
            var waveCourts = wave == Wave.AM ? amCourts : pmCourts;
            if (waveCourts.Count == 0) waveCourts = courts;

            var pairings = BuildRoundRobin(poolTeams.Select(t => t.Id).ToList());
            var refIndex = 0;
            foreach (var (homeId, awayId) in pairings)
            {
                Guid? stageId = null;
                if (poolStageIdByDivision.TryGetValue(pool.DivisionId, out var sid))
                    stageId = sid;

                var refCandidates = poolTeams.Where(t => t.Id != homeId && t.Id != awayId).Select(t => t.Id).ToList();
                Guid? refTeamId = refCandidates.Count > 0
                    ? refCandidates[refIndex++ % refCandidates.Count]
                    : null;

                var match = new Match(
                    tournament.OrganizationId,
                    tournamentId,
                    pool.DivisionId,
                    wave,
                    placeholderStart,
                    duration,
                    poolId: pool.Id,
                    courtId: null,
                    homeTeamId: homeId,
                    awayTeamId: awayId,
                    refTeamId: refTeamId,
                    stageId: stageId);
                _db.Matches.Add(match);
                created.Add(match);

                if (waveCourts.Count > 0)
                {
                    var slot = nextSlotByWave[wave];
                    var court = waveCourts[slot % waveCourts.Count];
                    var scheduledStart = waveStart.AddMinutes(duration * (slot / waveCourts.Count));
                    var scheduledEnd = scheduledStart.AddMinutes(duration);
                    assignments.Add(new MatchAssignment(
                        tournament.OrganizationId,
                        scheduleVersion.Id,
                        match.Id,
                        court.Id,
                        scheduledStart,
                        scheduledEnd));
                    nextSlotByWave[wave] = slot + 1;
                }
            }
        }

        foreach (var assignment in assignments)
            _db.MatchAssignments.Add(assignment);

        await _db.SaveChangesAsync(ct);
        _metrics.RecordScheduleGeneration(tournamentId, created.Count);
        return created;
    }

    private static List<(Guid, Guid)> BuildRoundRobin(List<Guid> teams)
    {
        var pairings = new List<(Guid, Guid)>();
        for (var i = 0; i < teams.Count; i++)
        {
            for (var j = i + 1; j < teams.Count; j++)
                pairings.Add((teams[i], teams[j]));
        }
        return pairings;
    }
}

using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Scheduling;

public class RoundRobinScheduleGenerator : IScheduleGenerator
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public RoundRobinScheduleGenerator(AppDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<Match>> GeneratePoolPlayAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var orgId = _tenant.RequireOrganizationId();

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
            ?? throw new InvalidOperationException("Tournament not found");

        var divisions = await _db.Divisions.Where(d => d.TournamentId == tournamentId).ToListAsync(ct);
        var pools = await _db.Pools
            .Where(p => divisions.Select(d => d.Id).Contains(p.DivisionId))
            .ToListAsync(ct);
        var teams = await _db.Teams
            .Where(t => divisions.Select(d => d.Id).Contains(t.DivisionId))
            .ToListAsync(ct);

        var existingPoolMatches = await _db.Matches
            .Where(m => m.TournamentId == tournamentId && m.PoolId != null && m.BracketNodeId == null)
            .ToListAsync(ct);
        _db.Matches.RemoveRange(existingPoolMatches);

        var courts = await _db.Courts
            .Where(c => _db.Venues.Where(v => v.TournamentId == tournamentId).Select(v => v.Id).Contains(c.VenueId))
            .ToListAsync(ct);

        var amCourts = courts.Where(c => !c.Tags.Any(t => t.Equals("PM", StringComparison.OrdinalIgnoreCase))).ToList();
        var pmCourts = courts.Where(c => !c.Tags.Any(t => t.Equals("AM", StringComparison.OrdinalIgnoreCase))).ToList();
        if (amCourts.Count == 0) amCourts = courts;
        if (pmCourts.Count == 0) pmCourts = courts;

        var duration = tournament.DefaultMatchDurationMinutes;
        var day = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var amStart = day.AddHours(8);
        var pmStart = day.AddHours(13);

        var created = new List<Match>();

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
            var slot = 0;
            foreach (var (homeId, awayId) in pairings)
            {
                var court = waveCourts[slot % Math.Max(1, waveCourts.Count)];
                var start = waveStart.AddMinutes(duration * (slot / Math.Max(1, waveCourts.Count)));
                var match = new Match(
                    orgId,
                    tournamentId,
                    pool.DivisionId,
                    wave,
                    start,
                    duration,
                    poolId: pool.Id,
                    courtId: court.Id,
                    homeTeamId: homeId,
                    awayTeamId: awayId);
                _db.Matches.Add(match);
                created.Add(match);
                slot++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return created;
    }

    private static List<(Guid, Guid)> BuildRoundRobin(List<Guid> teams)
    {
        var pairings = new List<(Guid, Guid)>();
        for (var i = 0; i < teams.Count; i++)
        {
            for (var j = i + 1; j < teams.Count; j++)
            {
                pairings.Add((teams[i], teams[j]));
            }
        }
        return pairings;
    }
}

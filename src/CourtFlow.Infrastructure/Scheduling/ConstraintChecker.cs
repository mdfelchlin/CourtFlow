using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Scheduling;

public class ConstraintChecker : IConstraintChecker
{
    private readonly AppDbContext _db;

    public ConstraintChecker(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ConflictReport> CheckTournamentAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var matches = await _db.Matches
            .Where(m => m.TournamentId == tournamentId && m.State != MatchState.Canceled)
            .Select(m => new
            {
                m.Id,
                m.TournamentId,
                m.CourtId,
                m.HomeTeamId,
                m.AwayTeamId,
                m.RefTeamId,
                m.Wave,
                m.ScheduledStartUtc,
                m.DurationMinutes
            })
            .ToListAsync(ct);

        var conflicts = new List<ScheduleConflict>();

        for (var i = 0; i < matches.Count; i++)
        {
            var a = matches[i];
            var aEnd = a.ScheduledStartUtc.AddMinutes(a.DurationMinutes);

            for (var j = i + 1; j < matches.Count; j++)
            {
                var b = matches[j];
                var bEnd = b.ScheduledStartUtc.AddMinutes(b.DurationMinutes);
                var overlaps = a.ScheduledStartUtc < bEnd && b.ScheduledStartUtc < aEnd;
                if (!overlaps) continue;

                if (a.CourtId != null && a.CourtId == b.CourtId)
                {
                    conflicts.Add(new ScheduleConflict(
                        ConflictKind.CourtDoubleBooking, a.Id, b.Id, null, a.CourtId, a.ScheduledStartUtc, aEnd,
                        $"Court double-booked at {a.ScheduledStartUtc:u}"));
                }

                Guid[] aTeams = new[] { a.HomeTeamId, a.AwayTeamId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
                Guid[] bTeams = new[] { b.HomeTeamId, b.AwayTeamId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();

                foreach (var team in aTeams.Intersect(bTeams))
                {
                    conflicts.Add(new ScheduleConflict(
                        ConflictKind.TeamDoubleBooking, a.Id, b.Id, team, null, a.ScheduledStartUtc, aEnd,
                        "Team scheduled in overlapping matches"));
                }

                if (a.RefTeamId is { } refA && bTeams.Contains(refA))
                {
                    conflicts.Add(new ScheduleConflict(
                        ConflictKind.RefConflict, a.Id, b.Id, refA, null, a.ScheduledStartUtc, aEnd,
                        "Referee team is playing in an overlapping match"));
                }
                if (b.RefTeamId is { } refB && aTeams.Contains(refB))
                {
                    conflicts.Add(new ScheduleConflict(
                        ConflictKind.RefConflict, b.Id, a.Id, refB, null, b.ScheduledStartUtc, bEnd,
                        "Referee team is playing in an overlapping match"));
                }
            }
        }

        var waveMismatches = await (from m in _db.Matches
                                    join h in _db.Teams on m.HomeTeamId equals h.Id into hj
                                    from h in hj.DefaultIfEmpty()
                                    join a in _db.Teams on m.AwayTeamId equals a.Id into aj
                                    from a in aj.DefaultIfEmpty()
                                    where m.TournamentId == tournamentId && m.State != MatchState.Canceled
                                          && ((h != null && h.Wave != m.Wave) || (a != null && a.Wave != m.Wave))
                                    select new { m.Id, m.CourtId, m.ScheduledStartUtc, m.DurationMinutes, TeamId = (h != null && h.Wave != m.Wave) ? h.Id : a!.Id })
            .ToListAsync(ct);

        foreach (var w in waveMismatches)
        {
            conflicts.Add(new ScheduleConflict(
                ConflictKind.WaveViolation, w.Id, null, w.TeamId, w.CourtId,
                w.ScheduledStartUtc, w.ScheduledStartUtc.AddMinutes(w.DurationMinutes),
                "Team wave does not match scheduled wave"));
        }

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct);
        if (tournament?.TournamentPartnerId is Guid partnerId)
        {
            var partnerMatches = await _db.Matches
                .Where(m => m.TournamentId == partnerId && m.State != MatchState.Canceled)
                .ToListAsync(ct);

            foreach (var a in matches)
            {
                var aEnd = a.ScheduledStartUtc.AddMinutes(a.DurationMinutes);
                var aTeams = new[] { a.HomeTeamId, a.AwayTeamId, a.RefTeamId }
                    .Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();

                foreach (var b in partnerMatches)
                {
                    var bEnd = b.ScheduledStartUtc.AddMinutes(b.DurationMinutes);
                    var overlaps = a.ScheduledStartUtc < bEnd && b.ScheduledStartUtc < aEnd;
                    if (!overlaps) continue;

                    var bTeams = new[] { b.HomeTeamId, b.AwayTeamId, b.RefTeamId }
                        .Where(x => x.HasValue).Select(x => x!.Value);

                    foreach (var team in bTeams.Where(aTeams.Contains))
                    {
                        conflicts.Add(new ScheduleConflict(
                            ConflictKind.PartnerTournamentConflict, a.Id, b.Id, team, a.CourtId,
                            a.ScheduledStartUtc, aEnd,
                            "Team has an overlapping match in a partner tournament"));
                    }
                }
            }
        }

        return new ConflictReport(conflicts);
    }

    public async Task<ConflictReport> CheckMatchAsync(Guid matchId, CancellationToken ct = default)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null) return new ConflictReport(Array.Empty<ScheduleConflict>());
        var report = await CheckTournamentAsync(match.TournamentId, ct);
        var filtered = report.Conflicts.Where(c => c.MatchId == matchId || c.OtherMatchId == matchId).ToList();
        return new ConflictReport(filtered);
    }
}

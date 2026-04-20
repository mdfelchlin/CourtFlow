using CourtFlow.Application.Abstractions;
using CourtFlow.Application.CourtStatus;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.CourtStatus;

public class CourtStatusService : ICourtStatusService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public CourtStatusService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CourtStatusView>> GetForTournamentAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var venues = await _db.Venues.Where(v => v.TournamentId == tournamentId).ToListAsync(ct);
        var venueIds = venues.Select(v => v.Id).ToList();
        var courts = await _db.Courts.Where(c => venueIds.Contains(c.VenueId)).ToListAsync(ct);

        var upcoming = await _db.Matches
            .Where(m => m.TournamentId == tournamentId && m.State != MatchState.Canceled)
            .ToListAsync(ct);
        var divisions = await _db.Divisions.Where(d => d.TournamentId == tournamentId).ToListAsync(ct);

        var result = new List<CourtStatusView>(courts.Count);

        foreach (var court in courts)
        {
            var venue = venues.First(v => v.Id == court.VenueId);

            var current = upcoming
                .Where(m => m.CourtId == court.Id && m.State != MatchState.Final)
                .OrderBy(m => m.ScheduledStartUtc)
                .FirstOrDefault();

            if (current is null)
            {
                result.Add(new CourtStatusView(court.Id, court.Name, venue.Name,
                    null, null, null, null, null, null, null, null, 0, CourtStatusKind.Idle));
                continue;
            }

            var division = divisions.FirstOrDefault(d => d.Id == current.DivisionId);
            var divisionName = division?.Name;

            var scheduled = current.ScheduledStartUtc;
            var reference = current.State == MatchState.InProgress
                ? (current.LastScoreAtUtc ?? current.ActualStartUtc ?? now)
                : now;

            var expectedFinish = scheduled.AddMinutes(current.DurationMinutes);
            var minutesBehind = (int)Math.Round((reference - expectedFinish).TotalMinutes);

            var status = CourtStatusKind.OnTime;
            if (minutesBehind <= -5) status = CourtStatusKind.Ahead;
            else if (minutesBehind >= 15) status = CourtStatusKind.Behind;
            else if (minutesBehind >= 5) status = CourtStatusKind.SlightlyBehind;

            var label = $"Match {current.Id:N}"[..Math.Min(20, 32)];
            result.Add(new CourtStatusView(
                court.Id, court.Name, venue.Name,
                current.Id, label, current.DivisionId, divisionName, current.Wave,
                current.ScheduledStartUtc, current.ActualStartUtc, current.LastScoreAtUtc,
                minutesBehind, status));
        }

        return result;
    }
}

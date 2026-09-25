using CourtFlow.Application.Abstractions;
using CourtFlow.Application.CourtStatus;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.ReadModels.MatchClockDelaySignal;
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

        var version = await ScheduleVersionProvisioning.GetLatestNonSupersededAsync(_db, tournamentId, ct);
        var assignments = version is null
            ? new List<Domain.Entities.MatchAssignment>()
            : await _db.MatchAssignments.Where(a => a.ScheduleVersionId == version.Id).ToListAsync(ct);
        var assignmentByMatch = assignments.ToDictionary(a => a.MatchId);

        var matches = await _db.Matches
            .Where(m => m.TournamentId == tournamentId && m.State != MatchState.Canceled)
            .ToListAsync(ct);
        var divisions = await _db.Divisions.Where(d => d.TournamentId == tournamentId).ToListAsync(ct);
        var teams = await _db.Teams.Where(t => divisions.Select(d => d.Id).Contains(t.DivisionId)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var result = new List<CourtStatusView>(courts.Count);

        foreach (var court in courts)
        {
            var venue = venues.First(v => v.Id == court.VenueId);

            var courtAssignments = assignments.Where(a => a.CourtId == court.Id).ToList();
            var courtMatchIds = courtAssignments.Select(a => a.MatchId).ToHashSet();
            var courtMatches = matches.Where(m => courtMatchIds.Contains(m.Id) || m.CourtId == court.Id).ToList();

            var current = courtMatches
                .Where(m => m.State != MatchState.Final)
                .Select(m =>
                {
                    assignmentByMatch.TryGetValue(m.Id, out var a);
                    var start = a?.ScheduledStart ?? m.ScheduledStartUtc;
                    return (Match: m, ScheduledStart: start);
                })
                .OrderBy(x => x.ScheduledStart)
                .FirstOrDefault();

            if (current.Match is null)
            {
                result.Add(new CourtStatusView(court.Id, court.Name, venue.Name,
                    null, null, null, null, null, null, null, null, 0, CourtStatusKind.Idle));
                continue;
            }

            var m = current.Match;
            var division = divisions.FirstOrDefault(d => d.Id == m.DivisionId);
            var divisionName = division?.Name;

            var scheduled = current.ScheduledStart;
            var delay = MatchClockDelaySignal.ComputeDelayMinutes(
                m.State, scheduled, m.ActualStartUtc, m.LastScoreAtUtc, now);
            var minutesBehind = delay ?? 0;

            var status = CourtStatusKind.OnTime;
            if (minutesBehind <= -5) status = CourtStatusKind.Ahead;
            else if (minutesBehind >= 15) status = CourtStatusKind.Behind;
            else if (minutesBehind >= 5) status = CourtStatusKind.SlightlyBehind;

            var home = m.HomeTeamId is { } h && teams.TryGetValue(h, out var hn) ? hn : "?";
            var away = m.AwayTeamId is { } aw && teams.TryGetValue(aw, out var an) ? an : "?";
            var label = $"{home} vs {away}";

            result.Add(new CourtStatusView(
                court.Id, court.Name, venue.Name,
                m.Id, label, m.DivisionId, divisionName, m.Wave,
                scheduled, m.ActualStartUtc, m.LastScoreAtUtc,
                minutesBehind, status));
        }

        return result;
    }
}

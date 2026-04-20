using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Public;

public sealed record ListPublicEventsQuery() : IRequest<IReadOnlyList<PublicEventDto>>;

public sealed class ListPublicEventsHandler : IRequestHandler<ListPublicEventsQuery, IReadOnlyList<PublicEventDto>>
{
    private readonly IAppDbContext _db;

    public ListPublicEventsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublicEventDto>> Handle(ListPublicEventsQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var tournaments = await _db.Tournaments
            .IgnoreQueryFilters()
            .Where(t => t.EndDate >= today)
            .OrderBy(t => t.StartDate)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        if (tournaments.Count == 0) return Array.Empty<PublicEventDto>();

        var tIds = tournaments.Select(t => t.Id).ToList();
        var orgIds = tournaments.Select(t => t.OrganizationId).Distinct().ToList();

        var orgs = await _db.Organizations
            .IgnoreQueryFilters()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, ct);

        var venueCounts = await _db.Venues
            .IgnoreQueryFilters()
            .Where(v => tIds.Contains(v.TournamentId))
            .GroupBy(v => v.TournamentId)
            .Select(g => new { TournamentId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var divisions = await _db.Divisions
            .IgnoreQueryFilters()
            .Where(d => tIds.Contains(d.TournamentId))
            .Select(d => new { d.Id, d.TournamentId })
            .ToListAsync(ct);

        var divByTourn = divisions.GroupBy(d => d.TournamentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var teamCounts = await _db.Teams
            .IgnoreQueryFilters()
            .Where(t => divisions.Select(d => d.Id).Contains(t.DivisionId))
            .GroupBy(t => t.DivisionId)
            .Select(g => new { DivisionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var teamCountByDiv = teamCounts.ToDictionary(x => x.DivisionId, x => x.Count);

        return tournaments.Select(t =>
        {
            var org = orgs.GetValueOrDefault(t.OrganizationId);
            var vCount = venueCounts.FirstOrDefault(v => v.TournamentId == t.Id)?.Count ?? 0;
            var divIds = divByTourn.GetValueOrDefault(t.Id) ?? new List<Guid>();
            var teamCount = divIds.Sum(d => teamCountByDiv.GetValueOrDefault(d, 0));
            return new PublicEventDto(
                t.Id,
                t.Name,
                t.Description,
                t.StartDate,
                t.EndDate,
                t.TimeZoneId,
                org?.Name ?? string.Empty,
                org?.Slug ?? string.Empty,
                vCount,
                divIds.Count,
                teamCount);
        }).ToList();
    }
}

public sealed record GetPublicEventQuery(Guid Id) : IRequest<PublicEventDetailDto?>;

public sealed class GetPublicEventHandler : IRequestHandler<GetPublicEventQuery, PublicEventDetailDto?>
{
    private readonly IAppDbContext _db;

    public GetPublicEventHandler(IAppDbContext db) => _db = db;

    public async Task<PublicEventDetailDto?> Handle(GetPublicEventQuery request, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;

        var org = await _db.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == t.OrganizationId, ct);

        var venues = await _db.Venues
            .IgnoreQueryFilters()
            .Where(v => v.TournamentId == t.Id)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);

        var venueIds = venues.Select(v => v.Id).ToList();
        var courts = await _db.Courts
            .IgnoreQueryFilters()
            .Where(c => venueIds.Contains(c.VenueId))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var divisions = await _db.Divisions
            .IgnoreQueryFilters()
            .Where(d => d.TournamentId == t.Id)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        var divIds = divisions.Select(d => d.Id).ToList();
        var teamsByDiv = (await _db.Teams
                .IgnoreQueryFilters()
                .Where(x => divIds.Contains(x.DivisionId))
                .Select(x => new { x.Id, x.DivisionId })
                .ToListAsync(ct))
            .GroupBy(x => x.DivisionId)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PublicEventDetailDto(
            t.Id,
            t.Name,
            t.Description,
            t.StartDate,
            t.EndDate,
            t.TimeZoneId,
            org?.Name ?? string.Empty,
            venues.Select(v => new PublicVenueDto(
                v.Id, v.Name, v.Address,
                courts.Where(c => c.VenueId == v.Id)
                    .Select(c => new PublicCourtDto(c.Id, c.Name))
                    .ToList())).ToList(),
            divisions.Select(d => new PublicDivisionDto(
                d.Id, d.Name, d.Format, teamsByDiv.GetValueOrDefault(d.Id, 0))).ToList());
    }
}

public sealed record GetPublicScheduleQuery(Guid EventId) : IRequest<IReadOnlyList<PublicMatchDto>?>;

public sealed class GetPublicScheduleHandler : IRequestHandler<GetPublicScheduleQuery, IReadOnlyList<PublicMatchDto>?>
{
    private readonly IAppDbContext _db;

    public GetPublicScheduleHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublicMatchDto>?> Handle(GetPublicScheduleQuery request, CancellationToken ct)
    {
        var exists = await _db.Tournaments
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == request.EventId, ct);
        if (!exists) return null;

        var matches = await _db.Matches
            .IgnoreQueryFilters()
            .Include(m => m.Sets)
            .Where(m => m.TournamentId == request.EventId)
            .OrderBy(m => m.ScheduledStartUtc)
            .ToListAsync(ct);

        var divIds = matches.Select(m => m.DivisionId).Distinct().ToList();
        var poolIds = matches.Where(m => m.PoolId.HasValue).Select(m => m.PoolId!.Value).Distinct().ToList();
        var courtIds = matches.Where(m => m.CourtId.HasValue).Select(m => m.CourtId!.Value).Distinct().ToList();
        var teamIds = matches
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var divisions = await _db.Divisions.IgnoreQueryFilters()
            .Where(d => divIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var pools = await _db.Pools.IgnoreQueryFilters()
            .Where(p => poolIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var courts = await _db.Courts.IgnoreQueryFilters()
            .Where(c => courtIds.Contains(c.Id)).ToListAsync(ct);
        var venueIds = courts.Select(c => c.VenueId).Distinct().ToList();
        var venues = await _db.Venues.IgnoreQueryFilters()
            .Where(v => venueIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Name, ct);
        var teams = await _db.Teams.IgnoreQueryFilters()
            .Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        return matches.Select(m =>
        {
            var court = m.CourtId.HasValue ? courts.FirstOrDefault(c => c.Id == m.CourtId.Value) : null;
            return new PublicMatchDto(
                m.Id,
                m.DivisionId,
                divisions.GetValueOrDefault(m.DivisionId, string.Empty),
                m.PoolId,
                m.PoolId.HasValue ? pools.GetValueOrDefault(m.PoolId.Value) : null,
                m.CourtId,
                court?.Name,
                court is null ? null : venues.GetValueOrDefault(court.VenueId),
                m.HomeTeamId,
                m.HomeTeamId.HasValue ? teams.GetValueOrDefault(m.HomeTeamId.Value) : null,
                m.AwayTeamId,
                m.AwayTeamId.HasValue ? teams.GetValueOrDefault(m.AwayTeamId.Value) : null,
                m.Wave,
                m.ScheduledStartUtc,
                m.DurationMinutes,
                m.ActualStartUtc,
                m.State,
                m.WinnerTeamId,
                m.Sets.OrderBy(s => s.SetNumber)
                    .Select(s => new PublicMatchSetDto(s.SetNumber, s.HomeScore, s.AwayScore))
                    .ToList());
        }).ToList();
    }
}

public sealed record GetPublicBracketsQuery(Guid EventId) : IRequest<IReadOnlyList<PublicBracketDto>?>;

public sealed class GetPublicBracketsHandler : IRequestHandler<GetPublicBracketsQuery, IReadOnlyList<PublicBracketDto>?>
{
    private readonly IAppDbContext _db;

    public GetPublicBracketsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublicBracketDto>?> Handle(GetPublicBracketsQuery request, CancellationToken ct)
    {
        var exists = await _db.Tournaments
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == request.EventId, ct);
        if (!exists) return null;

        var brackets = await _db.Brackets
            .IgnoreQueryFilters()
            .Where(b => b.TournamentId == request.EventId)
            .ToListAsync(ct);
        if (brackets.Count == 0) return Array.Empty<PublicBracketDto>();

        var bracketIds = brackets.Select(b => b.Id).ToList();
        var nodes = await _db.BracketNodes
            .IgnoreQueryFilters()
            .Where(n => bracketIds.Contains(n.BracketId))
            .ToListAsync(ct);

        var divIds = brackets.Select(b => b.DivisionId).Distinct().ToList();
        var divNames = await _db.Divisions.IgnoreQueryFilters()
            .Where(d => divIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var teamIds = nodes
            .SelectMany(n => new[] { n.HomeTeamId, n.AwayTeamId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var teamNames = await _db.Teams.IgnoreQueryFilters()
            .Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        return brackets.Select(b => new PublicBracketDto(
            b.Id,
            b.DivisionId,
            divNames.GetValueOrDefault(b.DivisionId, string.Empty),
            b.Type,
            b.IfNecessaryFinal,
            b.TeamCapacity,
            nodes.Where(n => n.BracketId == b.Id)
                .OrderBy(n => n.Round).ThenBy(n => n.Slot)
                .Select(n => new PublicBracketNodeDto(
                    n.Id,
                    n.Branch,
                    n.Round,
                    n.Slot,
                    n.MatchId,
                    n.HomeTeamId,
                    n.HomeTeamId.HasValue ? teamNames.GetValueOrDefault(n.HomeTeamId.Value) : null,
                    n.AwayTeamId,
                    n.AwayTeamId.HasValue ? teamNames.GetValueOrDefault(n.AwayTeamId.Value) : null,
                    n.HomeSeed,
                    n.AwaySeed,
                    n.WinsToNodeId,
                    n.LosesToNodeId))
                .ToList())).ToList();
    }
}

public sealed record GetPublicStandingsQuery(Guid EventId) : IRequest<IReadOnlyList<PublicPoolStandingsDto>?>;

public sealed class GetPublicStandingsHandler : IRequestHandler<GetPublicStandingsQuery, IReadOnlyList<PublicPoolStandingsDto>?>
{
    private readonly IAppDbContext _db;

    public GetPublicStandingsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublicPoolStandingsDto>?> Handle(GetPublicStandingsQuery request, CancellationToken ct)
    {
        var exists = await _db.Tournaments
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == request.EventId, ct);
        if (!exists) return null;

        var divisions = await _db.Divisions
            .IgnoreQueryFilters()
            .Where(d => d.TournamentId == request.EventId)
            .ToListAsync(ct);
        if (divisions.Count == 0) return Array.Empty<PublicPoolStandingsDto>();

        var divIds = divisions.Select(d => d.Id).ToList();

        var pools = await _db.Pools.IgnoreQueryFilters()
            .Where(p => divIds.Contains(p.DivisionId))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        if (pools.Count == 0) return Array.Empty<PublicPoolStandingsDto>();

        var teams = await _db.Teams.IgnoreQueryFilters()
            .Where(t => divIds.Contains(t.DivisionId))
            .ToListAsync(ct);

        var matches = await _db.Matches.IgnoreQueryFilters()
            .Where(m => divIds.Contains(m.DivisionId) && m.PoolId != null && m.State == MatchState.Final)
            .ToListAsync(ct);

        var matchIds = matches.Select(m => m.Id).ToHashSet();
        var sets = await _db.MatchSets.IgnoreQueryFilters()
            .Where(s => matchIds.Contains(s.MatchId))
            .ToListAsync(ct);

        var result = new List<PublicPoolStandingsDto>();

        foreach (var pool in pools)
        {
            var division = divisions.First(d => d.Id == pool.DivisionId);
            var poolTeams = teams.Where(t => t.PoolId == pool.Id).ToList();
            var poolMatches = matches.Where(m => m.PoolId == pool.Id).ToList();

            var rows = new List<PublicStandingsRowDto>();
            foreach (var team in poolTeams)
            {
                var played = poolMatches.Where(m => m.HomeTeamId == team.Id || m.AwayTeamId == team.Id).ToList();
                var wins = played.Count(m => m.WinnerTeamId == team.Id);
                var losses = played.Count - wins;

                var (setsWon, setsLost, pf, pa) = Aggregate(played, team.Id, sets);
                var setPct = (setsWon + setsLost) == 0 ? 0 : (double)setsWon / (setsWon + setsLost);
                var ptPct = (pf + pa) == 0 ? 0 : (double)pf / (pf + pa);

                rows.Add(new PublicStandingsRowDto(
                    team.Id, team.Name, 0, played.Count, wins, losses,
                    setsWon, setsLost, setPct, pf, pa, ptPct));
            }

            var ranked = rows
                .OrderByDescending(r => r.MatchWins)
                .ThenByDescending(r => r.SetPercentage)
                .ThenByDescending(r => r.PointPercentage)
                .Select((r, i) => r with { Rank = i + 1 })
                .ToList();

            result.Add(new PublicPoolStandingsDto(pool.Id, pool.Name, division.Id, division.Name, ranked));
        }

        return result;
    }

    private static (int setsWon, int setsLost, int pointsFor, int pointsAgainst) Aggregate(
        List<Domain.Entities.Match> matches, Guid teamId, List<Domain.Entities.MatchSet> sets)
    {
        var sw = 0; var sl = 0; var pf = 0; var pa = 0;
        foreach (var m in matches)
        {
            var isHome = m.HomeTeamId == teamId;
            foreach (var s in sets.Where(x => x.MatchId == m.Id))
            {
                var my = isHome ? s.HomeScore : s.AwayScore;
                var their = isHome ? s.AwayScore : s.HomeScore;
                pf += my; pa += their;
                if (my > their) sw++;
                else if (their > my) sl++;
            }
        }
        return (sw, sl, pf, pa);
    }
}

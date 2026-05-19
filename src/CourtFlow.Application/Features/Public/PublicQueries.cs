using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Brackets;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.ReadModels.PublicEventReadiness;
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
            .Where(t => t.PublicationStatus == TournamentPublicationStatus.Published && t.EndDate >= today)
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

public sealed record ListPastPublicEventsQuery() : IRequest<IReadOnlyList<PublicEventDto>>;

public sealed class ListPastPublicEventsHandler : IRequestHandler<ListPastPublicEventsQuery, IReadOnlyList<PublicEventDto>>
{
    private readonly IAppDbContext _db;

    public ListPastPublicEventsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublicEventDto>> Handle(ListPastPublicEventsQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var tournaments = await _db.Tournaments
            .IgnoreQueryFilters()
            .Where(t =>
                (t.PublicationStatus == TournamentPublicationStatus.Published
                 || t.PublicationStatus == TournamentPublicationStatus.Archived)
                && t.EndDate < today)
            .OrderByDescending(t => t.EndDate)
            .ThenByDescending(t => t.StartDate)
            .Take(50)
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

        var teamCounts = await _db.TournamentTeams
            .IgnoreQueryFilters()
            .Where(t => tIds.Contains(t.TournamentId))
            .GroupBy(t => t.TournamentId)
            .Select(g => new { TournamentId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var teamCountByTourn = teamCounts.ToDictionary(x => x.TournamentId, x => x.Count);

        return tournaments.Select(t =>
        {
            var org = orgs.GetValueOrDefault(t.OrganizationId);
            var vCount = venueCounts.FirstOrDefault(v => v.TournamentId == t.Id)?.Count ?? 0;
            var divIds = divByTourn.GetValueOrDefault(t.Id) ?? new List<Guid>();
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
                teamCountByTourn.GetValueOrDefault(t.Id, 0));
        }).ToList();
    }
}

public sealed record GetPublicEventReadinessQuery(Guid EventId) : IRequest<PublicEventReadinessDto?>;

public sealed class GetPublicEventReadinessHandler : IRequestHandler<GetPublicEventReadinessQuery, PublicEventReadinessDto?>
{
    private readonly IAppDbContext _db;
    private readonly IPublicEventReadiness _readiness;

    public GetPublicEventReadinessHandler(IAppDbContext db, IPublicEventReadiness readiness)
    {
        _db = db;
        _readiness = readiness;
    }

    public async Task<PublicEventReadinessDto?> Handle(GetPublicEventReadinessQuery request, CancellationToken ct)
    {
        var exists = await _db.Tournaments
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == request.EventId && t.PublicationStatus != TournamentPublicationStatus.Draft, ct);
        if (!exists) return null;
        return await _readiness.EvaluateAsync(request.EventId, ct);
    }
}

public sealed record GetPublicTeamsQuery(Guid EventId) : IRequest<IReadOnlyList<PublicDivisionTeamsDto>?>;

public sealed class GetPublicTeamsHandler : IRequestHandler<GetPublicTeamsQuery, IReadOnlyList<PublicDivisionTeamsDto>?>
{
    private readonly IAppDbContext _db;

    public GetPublicTeamsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublicDivisionTeamsDto>?> Handle(GetPublicTeamsQuery request, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == request.EventId, ct);
        if (t is null || t.PublicationStatus == TournamentPublicationStatus.Draft) return null;

        var divisions = await _db.Divisions
            .IgnoreQueryFilters()
            .Where(d => d.TournamentId == request.EventId)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        if (divisions.Count == 0) return Array.Empty<PublicDivisionTeamsDto>();

        var divIds = divisions.Select(d => d.Id).ToList();
        var teams = await _db.Teams.IgnoreQueryFilters()
            .Where(x => divIds.Contains(x.DivisionId))
            .ToListAsync(ct);
        var pools = await _db.Pools.IgnoreQueryFilters()
            .Where(p => divIds.Contains(p.DivisionId))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return divisions.Select(d => new PublicDivisionTeamsDto(
            d.Id,
            d.Name,
            teams.Where(t => t.DivisionId == d.Id)
                .OrderBy(t => t.Name)
                .Select(t => new PublicTeamRosterDto(
                    t.Id,
                    t.Name,
                    t.PoolId,
                    t.PoolId.HasValue ? pools.GetValueOrDefault(t.PoolId.Value) : null))
                .ToList())).ToList();
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
        if (t is null || t.PublicationStatus == TournamentPublicationStatus.Draft) return null;

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
        var t = await _db.Tournaments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.EventId, ct);
        if (t is null || t.PublicationStatus == TournamentPublicationStatus.Draft) return null;

        var publishedVersion = await _db.ScheduleVersions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.TournamentId == request.EventId && v.Status == ScheduleVersionStatus.Published, ct);
        if (publishedVersion is null) return Array.Empty<PublicMatchDto>();

        var assignments = await _db.MatchAssignments
            .IgnoreQueryFilters()
            .Where(a => a.ScheduleVersionId == publishedVersion.Id)
            .ToListAsync(ct);
        if (assignments.Count == 0) return Array.Empty<PublicMatchDto>();

        var matchIds = assignments.Select(a => a.MatchId).Distinct().ToList();
        var matches = await _db.Matches
            .IgnoreQueryFilters()
            .Include(m => m.Sets)
            .Where(m => matchIds.Contains(m.Id))
            .ToListAsync(ct);
        var assignmentByMatch = assignments.ToDictionary(a => a.MatchId);

        var divIds = matches.Select(m => m.DivisionId).Distinct().ToList();
        var poolIds = matches.Where(m => m.PoolId.HasValue).Select(m => m.PoolId!.Value).Distinct().ToList();
        var courtIds = assignments.Select(a => a.CourtId).Distinct().ToList();
        var teamIds = matches
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId, m.RefTeamId })
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

        return matches
            .Where(m => assignmentByMatch.ContainsKey(m.Id))
            .OrderBy(m => assignmentByMatch[m.Id].ScheduledStart)
            .Select(m =>
            {
                var a = assignmentByMatch[m.Id];
                var court = courts.FirstOrDefault(c => c.Id == a.CourtId);
                return new PublicMatchDto(
                    m.Id,
                    m.DivisionId,
                    divisions.GetValueOrDefault(m.DivisionId, string.Empty),
                    m.PoolId,
                    m.PoolId.HasValue ? pools.GetValueOrDefault(m.PoolId.Value) : null,
                    a.CourtId,
                    court?.Name,
                    court is null ? null : venues.GetValueOrDefault(court.VenueId),
                    m.HomeTeamId,
                    m.HomeTeamId.HasValue ? teams.GetValueOrDefault(m.HomeTeamId.Value) : null,
                    m.AwayTeamId,
                    m.AwayTeamId.HasValue ? teams.GetValueOrDefault(m.AwayTeamId.Value) : null,
                    m.RefTeamId,
                    m.RefTeamId.HasValue ? teams.GetValueOrDefault(m.RefTeamId.Value) : null,
                    m.Wave,
                    a.ScheduledStart,
                    (int)(a.ScheduledEnd - a.ScheduledStart).TotalMinutes,
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
        var t = await _db.Tournaments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.EventId, ct);
        if (t is null || t.PublicationStatus == TournamentPublicationStatus.Draft) return null;

        var brackets = await _db.Brackets
            .IgnoreQueryFilters()
            .Where(b => b.TournamentId == request.EventId)
            .ToListAsync(ct);
        if (brackets.Count == 0) return Array.Empty<PublicBracketDto>();

        var divIds = brackets.Select(b => b.DivisionId).Distinct().ToList();
        var divNames = await _db.Divisions.IgnoreQueryFilters()
            .Where(d => divIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var trees = await BracketTreeProjection.BuildManyAsync(
            _db,
            brackets,
            ignoreQueryFilters: true,
            publishedScheduleOnly: true,
            ct);

        return brackets.Select(b =>
        {
            var tree = trees.GetValueOrDefault(b.Id);
            var treeNodes = tree?.Nodes
                .OrderBy(n => n.Branch)
                .ThenBy(n => n.Round)
                .ThenBy(n => n.Slot)
                .Select(n => new PublicBracketNodeDto(
                    n.Id,
                    n.Branch,
                    n.Round,
                    n.Slot,
                    n.MatchId,
                    n.HomeTeamId,
                    n.HomeTeamId.HasValue && !n.HomeIsBye ? n.HomeLabel : null,
                    n.AwayTeamId,
                    n.AwayTeamId.HasValue && !n.AwayIsBye ? n.AwayLabel : null,
                    n.HomeSeed,
                    n.AwaySeed,
                    n.WinsToNodeId,
                    n.LosesToNodeId,
                    n.HomeLabel,
                    n.AwayLabel,
                    n.HomeIsBye,
                    n.AwayIsBye,
                    n.IsPinnedHome,
                    n.IsPinnedAway,
                    n.MatchState,
                    n.Sets.Select(s => new PublicBracketSetScoreDto(s.SetNumber, s.HomeScore, s.AwayScore)).ToList(),
                    n.HomeSetsSummary,
                    n.AwaySetsSummary,
                    n.WinnerTeamId,
                    n.WinnerSide,
                    n.CourtName,
                    n.ScheduledStartUtc,
                    n.MatchLabel))
                .ToList() ?? new List<PublicBracketNodeDto>();

            var groups = tree?.Groups
                .Select(g => new PublicBracketDisplayGroupDto(
                    g.Id,
                    g.Sequence,
                    g.Title,
                    g.Subtitle,
                    (PublicBracketDisplayGroupKind)(int)g.Kind,
                    g.NodeIds))
                .ToList() ?? new List<PublicBracketDisplayGroupDto>();

            return new PublicBracketDto(
                b.Id,
                b.DivisionId,
                divNames.GetValueOrDefault(b.DivisionId, string.Empty),
                b.Type,
                b.IfNecessaryFinal,
                b.TeamCapacity,
                b.DisplayName,
                b.ThirdPlaceMatch,
                treeNodes,
                groups);
        }).ToList();
    }
}

public sealed record GetPublicStandingsQuery(Guid EventId) : IRequest<IReadOnlyList<PublicPoolStandingsDto>?>;

public sealed class GetPublicStandingsHandler : IRequestHandler<GetPublicStandingsQuery, IReadOnlyList<PublicPoolStandingsDto>?>
{
    private readonly IAppDbContext _db;

    public GetPublicStandingsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PublicPoolStandingsDto>?> Handle(GetPublicStandingsQuery request, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.EventId, ct);
        if (t is null || t.PublicationStatus == TournamentPublicationStatus.Draft) return null;

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

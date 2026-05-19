using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Brackets;

public sealed record BracketDto(Guid Id, Guid TournamentId, Guid DivisionId, BracketType Type, bool IfNecessaryFinal, int TeamCapacity, string? DisplayName, bool ThirdPlaceMatch = false);
public sealed record BracketNodeDto(Guid Id, Guid BracketId, BracketBranch Branch, int Round, int Slot, Guid? MatchId, Guid? HomeTeamId, Guid? AwayTeamId, int? HomeSeed, int? AwaySeed, Guid? WinsToNodeId, Guid? LosesToNodeId);
public sealed record BracketWithNodesDto(BracketDto Bracket, IReadOnlyList<BracketNodeDto> Nodes);
public sealed record BracketTreeSetScoreDto(int SetNumber, int HomeScore, int AwayScore);
public sealed record BracketTreeNodeDto(
    Guid Id,
    Guid BracketId,
    BracketBranch Branch,
    int Round,
    int Slot,
    Guid? MatchId,
    Guid? HomeTeamId,
    Guid? AwayTeamId,
    int? HomeSeed,
    int? AwaySeed,
    Guid? WinsToNodeId,
    Guid? LosesToNodeId,
    string HomeLabel,
    string AwayLabel,
    bool HomeIsBye,
    bool AwayIsBye,
    bool IsPinnedHome,
    bool IsPinnedAway,
    MatchState? MatchState,
    IReadOnlyList<BracketTreeSetScoreDto> Sets,
    string? HomeSetsSummary,
    string? AwaySetsSummary,
    Guid? WinnerTeamId,
    string? WinnerSide,
    string? CourtName,
    DateTime? ScheduledStartUtc,
    string? MatchLabel = null)
{
    public string? HomeSetsAggregate => HomeSetsSummary;
    public string? AwaySetsAggregate => AwaySetsSummary;
}
public sealed record BracketTreeDto(
    BracketDto Bracket,
    IReadOnlyList<BracketTreeNodeDto> Nodes,
    IReadOnlyList<BracketDisplayGroupDto> Groups);

internal static class BracketMapping
{
    public static BracketDto ToDto(this Bracket b) => new(b.Id, b.TournamentId, b.DivisionId, b.Type, b.IfNecessaryFinal, b.TeamCapacity, b.DisplayName, b.ThirdPlaceMatch);
    public static BracketNodeDto ToDto(this BracketNode n) => new(
        n.Id, n.BracketId, n.Branch, n.Round, n.Slot, n.MatchId,
        n.HomeTeamId, n.AwayTeamId, n.HomeSeed, n.AwaySeed,
        n.WinsToNodeId, n.LosesToNodeId);

}

internal static class BracketTreeProjection
{
    public static async Task<BracketTreeDto?> BuildAsync(
        IAppDbContext db,
        Guid bracketId,
        CancellationToken ct)
    {
        var bracket = await db.Brackets.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bracketId, ct);
        if (bracket is null) return null;

        var trees = await BuildManyAsync(db, new[] { bracket }, ignoreQueryFilters: false, publishedScheduleOnly: false, ct);
        return trees.GetValueOrDefault(bracket.Id);
    }

    public static async Task<IReadOnlyDictionary<Guid, BracketTreeDto>> BuildManyAsync(
        IAppDbContext db,
        IReadOnlyList<Bracket> brackets,
        bool ignoreQueryFilters,
        bool publishedScheduleOnly,
        CancellationToken ct)
    {
        if (brackets.Count == 0) return new Dictionary<Guid, BracketTreeDto>();

        var bracketIds = brackets.Select(b => b.Id).ToList();
        var tournamentIds = brackets.Select(b => b.TournamentId).Distinct().ToList();
        var nodes = await Query(db.BracketNodes, ignoreQueryFilters)
            .Where(n => bracketIds.Contains(n.BracketId))
            .ToListAsync(ct);

        var matchIds = nodes.Where(n => n.MatchId.HasValue).Select(n => n.MatchId!.Value).Distinct().ToList();
        var matches = matchIds.Count == 0
            ? new Dictionary<Guid, Match>()
            : await Query(db.Matches, ignoreQueryFilters)
                .Include(m => m.Sets)
                .Where(m => matchIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, ct);

        var scheduleVersions = await Query(db.ScheduleVersions, ignoreQueryFilters)
            .Where(v => tournamentIds.Contains(v.TournamentId)
                && (publishedScheduleOnly
                    ? v.Status == ScheduleVersionStatus.Published
                    : v.Status != ScheduleVersionStatus.Superseded))
            .ToListAsync(ct);
        var scheduleVersionIds = scheduleVersions
            .GroupBy(v => v.TournamentId)
            .Select(g => g.OrderByDescending(v => v.CreatedAtUtc).First().Id)
            .ToList();
        var assignments = scheduleVersionIds.Count == 0 || matchIds.Count == 0
            ? new Dictionary<Guid, MatchAssignment>()
            : await Query(db.MatchAssignments, ignoreQueryFilters)
                .Where(a => scheduleVersionIds.Contains(a.ScheduleVersionId) && matchIds.Contains(a.MatchId))
                .GroupBy(a => a.MatchId)
                .Select(g => g.OrderByDescending(a => a.ScheduledStart).First())
                .ToDictionaryAsync(a => a.MatchId, ct);

        var teamIds = nodes
            .SelectMany(n => new[] { n.HomeTeamId, n.AwayTeamId })
            .Concat(matches.Values.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var teams = teamIds.Count == 0
            ? new Dictionary<Guid, Team>()
            : await Query(db.Teams, ignoreQueryFilters)
                .Where(t => teamIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, ct);

        var seedSlots = await Query(db.BracketSeedSlots, ignoreQueryFilters)
            .Where(s => bracketIds.Contains(s.BracketId))
            .ToListAsync(ct);
        var seedSlotIds = seedSlots.Select(s => s.Id).ToList();
        var overriddenSlotIds = seedSlotIds.Count == 0
            ? new HashSet<Guid>()
            : (await Query(db.BracketSeedSlotOverrides, ignoreQueryFilters)
                .Where(o => seedSlotIds.Contains(o.BracketSeedSlotId))
                .Select(o => o.BracketSeedSlotId)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet();
        var tournamentTeamIds = seedSlots
            .SelectMany(s => new[] { s.ManualTournamentTeamId, s.ResolvedTournamentTeamId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var tournamentTeams = tournamentTeamIds.Count == 0
            ? new Dictionary<Guid, TournamentTeam>()
            : await Query(db.TournamentTeams, ignoreQueryFilters)
                .Where(tt => tournamentTeamIds.Contains(tt.Id))
                .ToDictionaryAsync(tt => tt.Id, ct);
        var seedTeamIds = tournamentTeams.Values.Select(tt => tt.TeamId).Distinct().ToList();
        foreach (var seedTeam in await Query(db.Teams, ignoreQueryFilters).Where(t => seedTeamIds.Contains(t.Id)).ToListAsync(ct))
        {
            teams.TryAdd(seedTeam.Id, seedTeam);
        }

        var courtIds = matches.Values
            .Select(m => assignments.TryGetValue(m.Id, out var a) ? a.CourtId : m.CourtId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var courts = courtIds.Count == 0
            ? new Dictionary<Guid, Court>()
            : await Query(db.Courts, ignoreQueryFilters)
                .Where(c => courtIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);
        var venueIds = courts.Values.Select(c => c.VenueId).Distinct().ToList();
        var venues = venueIds.Count == 0
            ? new Dictionary<Guid, Venue>()
            : await Query(db.Venues, ignoreQueryFilters)
                .Where(v => venueIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, ct);

        var slotsByBracketAndSeed = seedSlots.ToDictionary(s => (s.BracketId, s.SeedPosition));

        return brackets.ToDictionary(
            b => b.Id,
            b =>
            {
                var bracketNodes = nodes.Where(n => n.BracketId == b.Id).ToList();
                var matchLabels = BracketMatchNumbering.BuildLabels(bracketNodes);
                var treeNodes = bracketNodes
                    .OrderBy(n => n.Branch)
                    .ThenBy(n => n.Round)
                    .ThenBy(n => n.Slot)
                    .Select(n => ToTreeNode(
                        n,
                        bracketNodes,
                        matchLabels,
                        matches,
                        assignments,
                        teams,
                        slotsByBracketAndSeed,
                        tournamentTeams,
                        overriddenSlotIds,
                        courts,
                        venues))
                    .ToList();
                return new BracketTreeDto(
                    b.ToDto(),
                    treeNodes,
                    BracketDisplayGrouper.BuildGroups(bracketNodes));
            });
    }

    private static BracketTreeNodeDto ToTreeNode(
        BracketNode node,
        IReadOnlyList<BracketNode> bracketNodes,
        IReadOnlyDictionary<Guid, string> matchLabels,
        IReadOnlyDictionary<Guid, Match> matches,
        IReadOnlyDictionary<Guid, MatchAssignment> assignments,
        IReadOnlyDictionary<Guid, Team> teams,
        IReadOnlyDictionary<(Guid BracketId, int SeedPosition), BracketSeedSlot> seedSlots,
        IReadOnlyDictionary<Guid, TournamentTeam> tournamentTeams,
        IReadOnlySet<Guid> overriddenSlotIds,
        IReadOnlyDictionary<Guid, Court> courts,
        IReadOnlyDictionary<Guid, Venue> venues)
    {
        var match = node.MatchId is { } matchId && matches.TryGetValue(matchId, out var m) ? m : null;
        var assignment = match is not null && assignments.TryGetValue(match.Id, out var a) ? a : null;
        var home = ResolveSide(node, bracketNodes, matchLabels, match, teams, seedSlots, tournamentTeams, overriddenSlotIds, side: 1);
        var away = ResolveSide(node, bracketNodes, matchLabels, match, teams, seedSlots, tournamentTeams, overriddenSlotIds, side: 2);
        var sets = match?.Sets
            .OrderBy(s => s.SetNumber)
            .Select(s => new BracketTreeSetScoreDto(s.SetNumber, s.HomeScore, s.AwayScore))
            .ToList() ?? new List<BracketTreeSetScoreDto>();

        var courtId = assignment?.CourtId ?? match?.CourtId;
        var court = courtId is { } cId && courts.TryGetValue(cId, out var foundCourt) ? foundCourt : null;
        var venueName = court is not null && venues.TryGetValue(court.VenueId, out var venue) ? venue.Name : null;

        return new BracketTreeNodeDto(
            node.Id,
            node.BracketId,
            node.Branch,
            node.Round,
            node.Slot,
            node.MatchId,
            node.HomeTeamId ?? match?.HomeTeamId,
            node.AwayTeamId ?? match?.AwayTeamId,
            node.HomeSeed,
            node.AwaySeed,
            node.WinsToNodeId,
            node.LosesToNodeId,
            home.Label,
            away.Label,
            home.IsBye,
            away.IsBye,
            home.IsPinned,
            away.IsPinned,
            match?.State,
            sets,
            sets.Count == 0 ? null : string.Join("-", sets.Select(s => s.HomeScore)),
            sets.Count == 0 ? null : string.Join("-", sets.Select(s => s.AwayScore)),
            match?.WinnerTeamId,
            WinnerSide(match),
            court is null ? null : venueName is null ? court.Name : $"{venueName} / {court.Name}",
            assignment?.ScheduledStart ?? match?.ScheduledStartUtc,
            matchLabels.GetValueOrDefault(node.Id));
    }

    private static (string Label, bool IsBye, bool IsPinned) ResolveSide(
        BracketNode node,
        IReadOnlyList<BracketNode> bracketNodes,
        IReadOnlyDictionary<Guid, string> matchLabels,
        Match? match,
        IReadOnlyDictionary<Guid, Team> teams,
        IReadOnlyDictionary<(Guid BracketId, int SeedPosition), BracketSeedSlot> seedSlots,
        IReadOnlyDictionary<Guid, TournamentTeam> tournamentTeams,
        IReadOnlySet<Guid> overriddenSlotIds,
        int side)
    {
        var teamId = side == 1
            ? node.HomeTeamId ?? match?.HomeTeamId
            : node.AwayTeamId ?? match?.AwayTeamId;
        if (teamId is { } resolvedTeamId && teams.TryGetValue(resolvedTeamId, out var team))
            return (team.Name, false, false);

        var seedPosition = node.Branch == BracketBranch.Winners && node.Round == 1
            ? (node.Slot * 2) + side
            : (int?)null;
        var slot = seedPosition.HasValue && seedSlots.TryGetValue((node.BracketId, seedPosition.Value), out var foundSlot)
            ? foundSlot
            : null;
        if (slot is null)
        {
            if (seedPosition.HasValue)
            {
                return ($"Seed {seedPosition.Value}", false, false);
            }

            return (
                BracketMatchNumbering.GetSideSourceLabel(node, side, bracketNodes, matchLabels),
                false,
                false);
        }

        var isPinned = slot.ManualTournamentTeamId.HasValue || overriddenSlotIds.Contains(slot.Id);
        var slotTournamentTeamId = slot.ManualTournamentTeamId ?? slot.ResolvedTournamentTeamId;
        if (slotTournamentTeamId is { } ttId
            && tournamentTeams.TryGetValue(ttId, out var tournamentTeam)
            && teams.TryGetValue(tournamentTeam.TeamId, out var seededTeam))
        {
            return (seededTeam.Name, false, isPinned);
        }

        if (slot.ExpectedSourceType.Equals("bye", StringComparison.OrdinalIgnoreCase))
            return ("BYE", true, isPinned);

        return (string.IsNullOrWhiteSpace(slot.Label) ? $"Seed {slot.SeedPosition}" : slot.Label!, false, isPinned);
    }

    private static string? WinnerSide(Match? match)
    {
        if (match?.WinnerTeamId is not { } winnerTeamId) return null;
        if (match.HomeTeamId == winnerTeamId) return "home";
        if (match.AwayTeamId == winnerTeamId) return "away";
        return null;
    }

    private static IQueryable<T> Query<T>(DbSet<T> set, bool ignoreQueryFilters) where T : class =>
        ignoreQueryFilters ? set.IgnoreQueryFilters().AsNoTracking() : set.AsNoTracking();
}

public sealed record ListBracketsQuery(Guid? DivisionId) : IRequest<IReadOnlyList<BracketDto>>;

public sealed class ListBracketsHandler : IRequestHandler<ListBracketsQuery, IReadOnlyList<BracketDto>>
{
    private readonly IAppDbContext _db;
    public ListBracketsHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<BracketDto>> Handle(ListBracketsQuery request, CancellationToken ct)
    {
        var q = _db.Brackets.AsQueryable();
        if (request.DivisionId is { } d) q = q.Where(b => b.DivisionId == d);
        var items = await q.ToListAsync(ct);
        return items.Select(BracketMapping.ToDto).ToList();
    }
}

public sealed record GetBracketQuery(Guid Id) : IRequest<BracketWithNodesDto?>;

public sealed class GetBracketHandler : IRequestHandler<GetBracketQuery, BracketWithNodesDto?>
{
    private readonly IAppDbContext _db;
    public GetBracketHandler(IAppDbContext db) => _db = db;
    public async Task<BracketWithNodesDto?> Handle(GetBracketQuery request, CancellationToken ct)
    {
        var b = await _db.Brackets.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (b is null) return null;
        var nodes = await _db.BracketNodes.Where(n => n.BracketId == b.Id).ToListAsync(ct);
        return new BracketWithNodesDto(b.ToDto(), nodes.Select(BracketMapping.ToDto).ToList());
    }
}

public sealed record GetBracketTreeQuery(Guid BracketId) : IRequest<BracketTreeDto?>;

public sealed class GetBracketTreeHandler : IRequestHandler<GetBracketTreeQuery, BracketTreeDto?>
{
    private readonly IAppDbContext _db;
    public GetBracketTreeHandler(IAppDbContext db) => _db = db;
    public Task<BracketTreeDto?> Handle(GetBracketTreeQuery request, CancellationToken ct) =>
        BracketTreeProjection.BuildAsync(_db, request.BracketId, ct);
}

public sealed record GenerateBracketCommand(Guid TournamentId, Guid DivisionId, BracketType Type, int TeamCapacity, bool IfNecessaryFinal, string? DisplayName = null, bool ThirdPlaceMatch = false) : IRequest<BracketDto>;

public sealed class GenerateBracketHandler : IRequestHandler<GenerateBracketCommand, BracketDto>
{
    private readonly IBracketBuilder _builder;
    public GenerateBracketHandler(IBracketBuilder builder) => _builder = builder;
    public async Task<BracketDto> Handle(GenerateBracketCommand request, CancellationToken ct)
    {
        var bracket = await _builder.BuildAsync(request.TournamentId, request.DivisionId,
            new BracketBuildOptions(request.Type, request.TeamCapacity, request.IfNecessaryFinal, request.DisplayName, request.ThirdPlaceMatch), ct);
        return bracket.ToDto();
    }
}

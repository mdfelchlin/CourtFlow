using CourtFlow.Application.Abstractions;
using CourtFlow.Application.ReadModels.DivisionValidation;
using CourtFlow.Application.Seeding;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.ReadModels;

public sealed class DivisionValidationService : IDivisionValidation
{
    private readonly IAppDbContext _db;

    public DivisionValidationService(IAppDbContext db) => _db = db;

    public async Task<DivisionValidationResultDto> EvaluateAsync(Guid divisionId, CancellationToken ct = default)
    {
        var issues = new List<DivisionValidationIssueDto>();

        var division = await _db.Divisions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == divisionId, ct);
        if (division is null)
            return new DivisionValidationResultDto(issues);

        var tournament = await _db.Tournaments.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == division.TournamentId, ct);

        var teams = await _db.Teams.AsNoTracking()
            .Where(t => t.DivisionId == divisionId)
            .ToListAsync(ct);
        var pools = await _db.Pools.AsNoTracking()
            .Where(p => p.DivisionId == divisionId)
            .OrderBy(p => p.Sequence)
            .ToListAsync(ct);
        var rankings = await _db.DivisionRankings.AsNoTracking()
            .Where(r => r.DivisionId == divisionId)
            .ToListAsync(ct);
        var ttByTeamId = await _db.TournamentTeams.AsNoTracking()
            .Where(tt => tt.DivisionId == divisionId)
            .ToDictionaryAsync(tt => tt.TeamId, tt => tt.Id, ct);

        CheckPoolLayout(issues, division.Format, teams.Count, pools);
        await CheckDuplicatePoolEntriesAsync(issues, divisionId, ct);
        await CheckUnplacedRankedTeamsAsync(issues, division.Format, divisionId, rankings, teams, ttByTeamId, ct);
        await CheckUnplacedInBracketAsync(issues, division.Format, divisionId, rankings, ct);
        await CheckPoolAndBracketOverlapAsync(issues, divisionId, division.Format, ct);
        await CheckBracketCapacityAsync(issues, division.Format, divisionId, ct);
        await CheckEmptyBracketSlotsAsync(issues, division.Format, divisionId, ct);
        await CheckEmptyMatchSlotsAsync(issues, divisionId, ct);
        await CheckMatchScheduleAsync(issues, divisionId, tournament, ct);
        await CheckMatchSlotGraphAsync(issues, divisionId, pools, teams.Count, ct);

        return new DivisionValidationResultDto(issues);
    }

    private static void CheckPoolLayout(
        List<DivisionValidationIssueDto> issues,
        DivisionFormat format,
        int teamCount,
        IReadOnlyList<Domain.Entities.Pool> pools)
    {
        if (format == DivisionFormat.BracketOnly) return;
        if (teamCount < 3 || pools.Count == 0) return;

        var suggested = PoolSeeding.BuildPoolSizes(teamCount);
        if (suggested.Count == 0) return;

        if (pools.Count != suggested.Count)
        {
            issues.Add(new DivisionValidationIssueDto(
                DivisionValidationSeverity.Warning,
                $"For {teamCount} teams, suggested layout is {suggested.Count} pool(s) ({string.Join(" + ", suggested)}); you have {pools.Count}.",
                "Pools"));
        }
    }

    private async Task CheckDuplicatePoolEntriesAsync(
        List<DivisionValidationIssueDto> issues,
        Guid divisionId,
        CancellationToken ct)
    {
        var poolIds = await _db.Pools.AsNoTracking()
            .Where(p => p.DivisionId == divisionId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (poolIds.Count == 0) return;

        var entries = await _db.PoolEntries.AsNoTracking()
            .Where(e => poolIds.Contains(e.PoolId))
            .ToListAsync(ct);

        var dup = entries.GroupBy(e => e.TournamentTeamId).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
        {
            issues.Add(new DivisionValidationIssueDto(
                DivisionValidationSeverity.Warning,
                "A team appears in more than one pool entry.",
                "Pools"));
        }
    }

    private async Task CheckUnplacedRankedTeamsAsync(
        List<DivisionValidationIssueDto> issues,
        DivisionFormat format,
        Guid divisionId,
        IReadOnlyList<Domain.Entities.DivisionRanking> rankings,
        IReadOnlyList<Domain.Entities.Team> teams,
        Dictionary<Guid, Guid> ttByTeamId,
        CancellationToken ct)
    {
        if (rankings.Count == 0) return;
        if (format is not (DivisionFormat.PoolOnly or DivisionFormat.PoolPlusBracket)) return;

        var poolStage = await _db.Stages.AsNoTracking()
            .Where(s => s.DivisionId == divisionId && s.StageType == StageType.Pool)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);
        if (poolStage?.Status == StageStatus.Draft) return;

        var poolIds = await _db.Pools.AsNoTracking()
            .Where(p => p.DivisionId == divisionId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (poolIds.Count == 0) return;

        var entries = await _db.PoolEntries.AsNoTracking()
            .Where(e => poolIds.Contains(e.PoolId))
            .ToListAsync(ct);
        if (entries.Count == 0) return;

        var ttToTeamId = ttByTeamId.ToDictionary(kv => kv.Value, kv => kv.Key);
        var placedTeamIds = teams.Where(t => t.PoolId is not null).Select(t => t.Id).ToHashSet();
        var unplaced = rankings.Count(r =>
            ttToTeamId.TryGetValue(r.TournamentTeamId, out var teamId) && !placedTeamIds.Contains(teamId));

        if (unplaced > 0)
        {
            issues.Add(new DivisionValidationIssueDto(
                DivisionValidationSeverity.Warning,
                $"{unplaced} ranked team(s) are not assigned to a pool.",
                "Pools"));
        }
    }

    private async Task CheckUnplacedInBracketAsync(
        List<DivisionValidationIssueDto> issues,
        DivisionFormat format,
        Guid divisionId,
        IReadOnlyList<Domain.Entities.DivisionRanking> rankings,
        CancellationToken ct)
    {
        if (format != DivisionFormat.BracketOnly || rankings.Count == 0) return;

        var bracketStage = await _db.Stages.AsNoTracking()
            .Where(s => s.DivisionId == divisionId && s.StageType == StageType.Bracket)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);
        if (bracketStage?.Status == StageStatus.Draft) return;

        var seedSlots = await _db.BracketSeedSlots.AsNoTracking()
            .Where(s => _db.Brackets.Any(b => b.Id == s.BracketId && b.DivisionId == divisionId))
            .ToListAsync(ct);
        if (seedSlots.Count == 0) return;

        var seeded = seedSlots
            .Where(s => s.ResolvedTournamentTeamId != null)
            .Select(s => s.ResolvedTournamentTeamId!.Value)
            .ToHashSet();
        if (seeded.Count == 0) return;

        var unplaced = rankings.Count(r => !seeded.Contains(r.TournamentTeamId));
        if (unplaced > 0)
        {
            issues.Add(new DivisionValidationIssueDto(
                DivisionValidationSeverity.Warning,
                $"{unplaced} ranked team(s) are not seeded into the bracket.",
                "Bracket"));
        }
    }

    private async Task CheckPoolAndBracketOverlapAsync(
        List<DivisionValidationIssueDto> issues,
        Guid divisionId,
        DivisionFormat format,
        CancellationToken ct)
    {
        if (format != DivisionFormat.PoolPlusBracket) return;

        var poolIds = await _db.Pools.AsNoTracking()
            .Where(p => p.DivisionId == divisionId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (poolIds.Count == 0) return;

        var inPool = (await _db.PoolEntries.AsNoTracking()
            .Where(e => poolIds.Contains(e.PoolId))
            .Select(e => e.TournamentTeamId)
            .ToListAsync(ct)).ToHashSet();

        var inBracket = await _db.BracketSeedSlots.AsNoTracking()
            .Where(s => _db.Brackets.Any(b => b.Id == s.BracketId && b.DivisionId == divisionId)
                        && s.ResolvedTournamentTeamId != null)
            .Select(s => s.ResolvedTournamentTeamId!.Value)
            .ToListAsync(ct);

        if (inPool.Intersect(inBracket).Any())
        {
            issues.Add(new DivisionValidationIssueDto(
                DivisionValidationSeverity.Warning,
                "A team appears in both a pool and a bracket seed slot.",
                "Placement"));
        }
    }

    private async Task CheckEmptyBracketSlotsAsync(
        List<DivisionValidationIssueDto> issues,
        DivisionFormat format,
        Guid divisionId,
        CancellationToken ct)
    {
        if (format == DivisionFormat.PoolOnly) return;

        var brackets = await _db.Brackets.AsNoTracking()
            .Where(b => b.DivisionId == divisionId)
            .ToListAsync(ct);
        foreach (var bracket in brackets)
        {
            var empty = await _db.BracketSeedSlots.AsNoTracking()
                .CountAsync(s => s.BracketId == bracket.Id && s.ResolvedTournamentTeamId == null, ct);
            if (empty > 0)
            {
                issues.Add(new DivisionValidationIssueDto(
                    DivisionValidationSeverity.Info,
                    $"Bracket \"{bracket.DisplayName ?? "Bracket"}\" has {empty} empty seed slot(s).",
                    "Bracket"));
            }
        }
    }

    private async Task CheckEmptyMatchSlotsAsync(
        List<DivisionValidationIssueDto> issues,
        Guid divisionId,
        CancellationToken ct)
    {
        var matches = await _db.Matches.AsNoTracking()
            .Where(m => m.DivisionId == divisionId && m.State == MatchState.NotStarted)
            .ToListAsync(ct);
        if (matches.Count == 0) return;

        var matchIds = matches.Select(m => m.Id).ToList();
        var sources = await _db.MatchSlotSources.AsNoTracking()
            .Where(s => matchIds.Contains(s.MatchId))
            .ToListAsync(ct);
        var sourcesByMatch = sources.GroupBy(s => s.MatchId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var match in matches)
        {
            var slotSources = sourcesByMatch.GetValueOrDefault(match.Id) ?? [];
            var homeFilled = match.HomeTeamId is not null
                || slotSources.Any(s => s.Slot == MatchSlotKind.Home);
            var awayFilled = match.AwayTeamId is not null
                || slotSources.Any(s => s.Slot == MatchSlotKind.Away);
            if (!homeFilled || !awayFilled)
            {
                issues.Add(new DivisionValidationIssueDto(
                    DivisionValidationSeverity.Info,
                    "A scheduled match has an unfilled home or away slot.",
                    "Match graph"));
                break;
            }
        }
    }

    private async Task CheckBracketCapacityAsync(
        List<DivisionValidationIssueDto> issues,
        DivisionFormat format,
        Guid divisionId,
        CancellationToken ct)
    {
        if (format == DivisionFormat.PoolOnly) return;
        var brackets = await _db.Brackets.AsNoTracking()
            .Where(b => b.DivisionId == divisionId)
            .ToListAsync(ct);
        foreach (var bracket in brackets)
        {
            var size = BracketSeedOrder.NextPowerOfTwo(bracket.TeamCapacity);
            if (size != bracket.TeamCapacity)
            {
                issues.Add(new DivisionValidationIssueDto(
                    DivisionValidationSeverity.Info,
                    $"Bracket \"{bracket.DisplayName ?? "Bracket"}\" capacity {bracket.TeamCapacity} will use {size} slots (byes for empty seeds).",
                    "Bracket"));
            }

            var filled = await _db.BracketSeedSlots.AsNoTracking()
                .CountAsync(s => s.BracketId == bracket.Id && s.ResolvedTournamentTeamId != null, ct);
            if (filled > 0 && filled < bracket.TeamCapacity)
            {
                issues.Add(new DivisionValidationIssueDto(
                    DivisionValidationSeverity.Info,
                    $"Bracket \"{bracket.DisplayName ?? "Bracket"}\" has {filled} seeded team(s) of {bracket.TeamCapacity} slots.",
                    "Bracket"));
            }
        }
    }

    private async Task CheckMatchScheduleAsync(
        List<DivisionValidationIssueDto> issues,
        Guid divisionId,
        Domain.Entities.Tournament? tournament,
        CancellationToken ct)
    {
        if (tournament is null) return;

        var matches = await _db.Matches.AsNoTracking()
            .Where(m => m.DivisionId == divisionId)
            .ToListAsync(ct);

        var tournamentStart = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var tournamentEnd = tournament.EndDate.ToDateTime(new TimeOnly(23, 59), DateTimeKind.Utc);

        foreach (var match in matches)
        {
            if (match.ScheduledStartUtc < tournamentStart || match.ScheduledStartUtc > tournamentEnd)
            {
                issues.Add(new DivisionValidationIssueDto(
                    DivisionValidationSeverity.Warning,
                    "A match is scheduled outside the tournament date range.",
                    "Schedule"));
            }
        }

        var byCourt = matches
            .Where(m => m.CourtId is not null && m.State is not MatchState.Canceled)
            .GroupBy(m => m.CourtId!.Value);
        foreach (var group in byCourt)
        {
            var ordered = group.OrderBy(m => m.ScheduledStartUtc).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var cur = ordered[i];
                if (cur.ScheduledStartUtc < prev.ScheduledEndUtc)
                {
                    issues.Add(new DivisionValidationIssueDto(
                        DivisionValidationSeverity.Warning,
                        "Two matches overlap on the same court.",
                        "Schedule"));
                    break;
                }
            }
        }
    }

    private async Task CheckMatchSlotGraphAsync(
        List<DivisionValidationIssueDto> issues,
        Guid divisionId,
        IReadOnlyList<Domain.Entities.Pool> pools,
        int teamCount,
        CancellationToken ct)
    {
        var slotSources = await _db.MatchSlotSources.AsNoTracking()
            .Where(s => _db.Matches.Any(m => m.Id == s.MatchId && m.DivisionId == divisionId))
            .ToListAsync(ct);
        if (slotSources.Count == 0) return;

        var matchIds = slotSources.Select(s => s.MatchId).Distinct().ToHashSet();
        var matches = await _db.Matches.AsNoTracking()
            .Where(m => matchIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        foreach (var src in slotSources)
        {
            if (src.SourceKind == MatchSlotSourceKind.PoolFinish)
            {
                var pool = pools.FirstOrDefault(p => p.Id == src.SourcePoolId);
                if (pool is null)
                {
                    issues.Add(new DivisionValidationIssueDto(
                        DivisionValidationSeverity.Warning,
                        "A match slot references a pool that no longer exists.",
                        "Match graph"));
                    continue;
                }

                if (src.SourcePosition is not { } finish || finish > pool.TargetSize)
                {
                    issues.Add(new DivisionValidationIssueDto(
                        DivisionValidationSeverity.Warning,
                        $"Match slot references finish position {src.SourcePosition} in pool \"{pool.Name}\" (target size {pool.TargetSize}).",
                        "Match graph"));
                }
            }

            if (src.SourceKind == MatchSlotSourceKind.Ranking
                && src.SourcePosition is { } rank
                && rank > teamCount)
            {
                issues.Add(new DivisionValidationIssueDto(
                    DivisionValidationSeverity.Warning,
                    $"Match slot references seed rank {rank} but division has {teamCount} team(s).",
                    "Match graph"));
            }

            if (src.SourceKind is MatchSlotSourceKind.MatchWinner or MatchSlotSourceKind.MatchLoser
                && src.SourceMatchId is { } sourceId
                && !matches.ContainsKey(sourceId))
            {
                issues.Add(new DivisionValidationIssueDto(
                    DivisionValidationSeverity.Warning,
                    "A match slot references a source match that does not exist.",
                    "Match graph"));
            }
        }

        var edges = slotSources
            .Where(s => s.SourceMatchId is not null
                        && s.SourceKind is MatchSlotSourceKind.MatchWinner or MatchSlotSourceKind.MatchLoser)
            .Select(s => (From: s.SourceMatchId!.Value, To: s.MatchId))
            .ToList();

        if (HasCycle(edges.Select(e => e.To).Distinct(), edges))
        {
            issues.Add(new DivisionValidationIssueDto(
                DivisionValidationSeverity.Warning,
                "Match connection graph contains a cycle — advancement may never resolve.",
                "Match graph"));
        }

        var fixedTeams = slotSources
            .Where(s => s.SourceKind == MatchSlotSourceKind.FixedTeam && s.FixedTournamentTeamId is not null)
            .GroupBy(s => s.FixedTournamentTeamId!.Value)
            .Where(g => g.Count() > 1)
            .ToList();
        if (fixedTeams.Count > 0)
        {
            issues.Add(new DivisionValidationIssueDto(
                DivisionValidationSeverity.Warning,
                "The same team is fixed into multiple match slots.",
                "Match graph"));
        }
    }

    private static bool HasCycle(IEnumerable<Guid> nodes, IReadOnlyList<(Guid From, Guid To)> edges)
    {
        var adj = nodes.ToDictionary(n => n, _ => new List<Guid>());
        foreach (var (from, to) in edges)
        {
            if (!adj.ContainsKey(from)) adj[from] = new List<Guid>();
            if (!adj.ContainsKey(to)) adj[to] = new List<Guid>();
            adj[from].Add(to);
        }

        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();

        bool Dfs(Guid node)
        {
            if (visiting.Contains(node)) return true;
            if (visited.Contains(node)) return false;
            visiting.Add(node);
            foreach (var next in adj.GetValueOrDefault(node, []))
            {
                if (Dfs(next)) return true;
            }
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }

        return adj.Keys.Any(Dfs);
    }
}

using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Persistence;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.Features.Teams;
using CourtFlow.Application.Matches;
using CourtFlow.Application.Seeding;
using CourtFlow.Application.Standings;
using CourtFlow.Application.StageMaterialization;
using CourtFlow.Application.WorkTeams;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Brackets;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.StageMaterialization;

public sealed class StageMaterializationService : IStageMaterialization
{
    private readonly IAppDbContext _db;
    private readonly IStandingsCalculator _standings;

    public StageMaterializationService(IAppDbContext db, IStandingsCalculator standings)
    {
        _db = db;
        _standings = standings;
    }

    public async Task SnakeIntoPoolsAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == divisionId, cancellationToken)
            ?? throw new InvalidOperationException("Division not found.");

        var poolStage = await _db.Stages
            .Where(s => s.DivisionId == divisionId && s.StageType == StageType.Pool)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Pool stage not found.");

        if (poolStage.Status is StageStatus.InProgress or StageStatus.Completed)
            throw new InvalidOperationException("Pool stage is in progress or completed — use Repairs to change placements.");

        var poolSettings = await _db.PoolSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StageId == poolStage.Id, cancellationToken);
        var seedingMethod = poolSettings?.SeedingMethod ?? "snake";
        if (!string.Equals(seedingMethod, "snake", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Seeding method '{seedingMethod}' is not supported yet.");

        var teams = await _db.Teams.Where(t => t.DivisionId == divisionId).ToListAsync(cancellationToken);
        if (teams.Count < 3)
            throw new InvalidOperationException("At least 3 teams are required in rankings.");

        await BackfillDivisionRankingsAsync(division, teams, cancellationToken);

        var rankByTeam = new Dictionary<Guid, int>(
            await DivisionEntryRankingService.GetRankByTeamIdAsync(_db, division.Id, cancellationToken));
        var ordered = teams
            .OrderBy(t => rankByTeam.TryGetValue(t.Id, out var rk) ? rk : int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var poolSizes = PoolSeeding.BuildPoolSizes(ordered.Count);
        if (poolSizes.Count == 0)
            throw new InvalidOperationException($"Cannot derive pool layout for {ordered.Count} teams.");

        var divisionPools = await EnsurePoolsAsync(division, poolSizes.Count, cancellationToken);
        var poolSequence = PoolSeeding.BuildSnakePoolSequence(poolSizes);

        var poolIds = divisionPools.Select(p => p.Id).ToList();
        await EntityGraphDeletes.BulkDeleteQueryAsync(
            _db,
            _db.PoolEntries.Where(e => poolIds.Contains(e.PoolId)),
            cancellationToken);

        var seedCounters = new Dictionary<Guid, int>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var pool = divisionPools[poolSequence[i]];
            ordered[i].AssignPool(pool.Id);

            var tournamentTeam = await DivisionEntryRankingService.EnsureTournamentTeamAsync(
                _db, division, ordered[i], cancellationToken);

            var seedInPool = seedCounters.TryGetValue(pool.Id, out var c) ? c + 1 : 1;
            seedCounters[pool.Id] = seedInPool;

            _db.PoolEntries.Add(new PoolEntry(
                division.OrganizationId,
                pool.Id,
                tournamentTeam.Id,
                seedInPool));
        }

        if (poolStage.Status == StageStatus.Draft)
            poolStage.TransitionTo(StageStatus.Configured);

        if (poolStage.Status == StageStatus.Configured)
            poolStage.TransitionTo(StageStatus.Seeded);

        await ReplaceDivisionPoolMatchesAsync(division, poolStage, poolSettings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task GenerateBracketsAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == divisionId, cancellationToken)
            ?? throw new InvalidOperationException("Division not found.");

        var (poolStage, bracketStage) = await BracketMaterializationHelper.RequireStagesAsync(_db, divisionId, cancellationToken);

        if (bracketStage.Status is StageStatus.InProgress or StageStatus.Completed)
            throw new InvalidOperationException("Bracket stage is in progress or completed — use Repairs to change structure.");

        var brackets = await _db.Brackets.Where(b => b.DivisionId == divisionId).OrderBy(b => b.DisplayName).ToListAsync(cancellationToken);
        if (brackets.Count == 0)
            throw new InvalidOperationException("Define bracket structure in Step 2 first.");

        var rules = await _db.AdvancementRules.Where(r => r.SourceStageId == poolStage.Id).ToListAsync(cancellationToken);
        var pools = await _db.Pools.Where(p => p.DivisionId == divisionId).OrderBy(p => p.Sequence).ToListAsync(cancellationToken);

        foreach (var bracket in brackets)
        {
            var existingSlots = await _db.BracketSeedSlots
                .AsNoTracking()
                .Where(s => s.BracketId == bracket.Id)
                .ToListAsync(cancellationToken);
            var manualByPosition = existingSlots.ToDictionary(s => s.SeedPosition, s => s.ManualTournamentTeamId);
            await EntityGraphDeletes.ClearBracketSeedSlotsAsync(_db, bracket.Id, cancellationToken);

            var bracketRules = rules.Where(r => r.TargetBracketId == bracket.Id).ToList();
            for (var pos = 1; pos <= bracket.TeamCapacity; pos++)
            {
                var rule = FindRuleForSlot(bracketRules, pos, pools.Count);
                var label = $"{bracket.DisplayName ?? "Bracket"}-{pos}";
                var slot = new BracketSeedSlot(
                    division.OrganizationId,
                    bracket.Id,
                    pos,
                    rule is null && manualByPosition.ContainsKey(pos) ? "manual" : "advancement_rule",
                    label);
                slot.LinkAdvancementRule(rule?.Id);
                if (manualByPosition.TryGetValue(pos, out var manual))
                    slot.SetManualOverride(manual);
                _db.BracketSeedSlots.Add(slot);
            }

            var hasNodes = await _db.BracketNodes.AnyAsync(n => n.BracketId == bracket.Id, cancellationToken);
            if (!hasNodes)
                await EnsureBracketNodesAsync(division.OrganizationId, bracket, cancellationToken);

            await EnsureAllBracketNodeMatchesAsync(division, bracketStage, bracket, cancellationToken);
            await MatchSlotSourceSync.SyncBracketAdvancementSlotsAsync(
                _db, division.OrganizationId, bracket.Id, cancellationToken);
        }

        if (bracketStage.Status == StageStatus.Draft)
            bracketStage.TransitionTo(StageStatus.Configured);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveBracketSlotsAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == divisionId, cancellationToken)
            ?? throw new InvalidOperationException("Division not found.");

        var (poolStage, bracketStage) = await BracketMaterializationHelper.RequireStagesAsync(_db, divisionId, cancellationToken);
        var brackets = await _db.Brackets.Where(b => b.DivisionId == divisionId).ToListAsync(cancellationToken);
        if (brackets.Count == 0)
            throw new InvalidOperationException("Generate brackets in Step 2 first.");

        await BracketMaterializationHelper.SyncFinishPositionsFromStandingsAsync(_db, _standings, divisionId, cancellationToken);

        var pools = await _db.Pools.Where(p => p.DivisionId == divisionId).OrderBy(p => p.Sequence).ToListAsync(cancellationToken);
        var entries = await _db.PoolEntries.Where(e => pools.Select(p => p.Id).Contains(e.PoolId)).ToListAsync(cancellationToken);
        var rules = await _db.AdvancementRules.Where(r => r.SourceStageId == poolStage.Id).ToListAsync(cancellationToken);
        var teamIdByTournamentTeamId = await BracketMaterializationHelper.TeamIdByTournamentTeamIdAsync(_db, divisionId, cancellationToken);

        var finishByPool = entries
            .Where(e => e.FinishPosition is not null)
            .ToDictionary(e => (e.PoolId, e.FinishPosition!.Value), e => e.TournamentTeamId);

        var wildcardBuckets = BuildWildcardBuckets(entries, pools, rules);

        foreach (var bracket in brackets)
        {
            var slots = await _db.BracketSeedSlots.Where(s => s.BracketId == bracket.Id).OrderBy(s => s.SeedPosition).ToListAsync(cancellationToken);
            if (slots.Count == 0)
                throw new InvalidOperationException("Generate brackets before resolving slots.");

            foreach (var slot in slots)
            {
                var tournamentTeamId = BracketMaterializationHelper.ResolveSlotTournamentTeamId(
                    slot, rules, pools, finishByPool, wildcardBuckets);
                slot.Resolve(tournamentTeamId);
            }

            await ApplyResolvedTeamsToRoundOneAsync(bracket, slots, teamIdByTournamentTeamId, cancellationToken);
            await MatchSlotSourceSync.SyncBracketAdvancementSlotsAsync(
                _db, division.OrganizationId, bracket.Id, cancellationToken);
            await EnsureBracketAdvancementMatchesAndByesAsync(division, bracketStage, bracket, cancellationToken);
        }

        if (bracketStage.Status == StageStatus.Configured)
            bracketStage.TransitionTo(StageStatus.Seeded);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedBracketFromRankingsAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == divisionId, cancellationToken)
            ?? throw new InvalidOperationException("Division not found.");

        var bracketStage = await BracketMaterializationHelper.RequireBracketStageAsync(_db, divisionId, cancellationToken);

        if (bracketStage.Status is StageStatus.InProgress or StageStatus.Completed)
            throw new InvalidOperationException("Bracket stage is in progress or completed — use Repairs to change structure.");

        var teams = await _db.Teams.Where(t => t.DivisionId == divisionId).ToListAsync(cancellationToken);
        if (teams.Count < 2)
            throw new InvalidOperationException("At least 2 teams are required in rankings.");

        await BackfillDivisionRankingsAsync(division, teams, cancellationToken);

        var rankByTeam = new Dictionary<Guid, int>(
            await DivisionEntryRankingService.GetRankByTeamIdAsync(_db, division.Id, cancellationToken));
        var orderedTeams = teams
            .OrderBy(t => rankByTeam.TryGetValue(t.Id, out var rk) ? rk : int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderedTournamentTeamIds = new List<Guid>();
        foreach (var team in orderedTeams)
        {
            var tt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(
                _db, division, team, cancellationToken);
            orderedTournamentTeamIds.Add(tt.Id);
        }

        var brackets = await _db.Brackets.Where(b => b.DivisionId == divisionId).OrderBy(b => b.DisplayName).ToListAsync(cancellationToken);
        if (brackets.Count == 0)
            throw new InvalidOperationException("Define and save bracket structure first.");

        var teamIdByTournamentTeamId = await BracketMaterializationHelper.TeamIdByTournamentTeamIdAsync(
            _db, divisionId, cancellationToken);

        foreach (var bracket in brackets)
        {
            var capacity = Math.Max(bracket.TeamCapacity, orderedTournamentTeamIds.Count);
            var bracketSize = BracketSeedOrder.NextPowerOfTwo(capacity);
            if (bracketSize != bracket.TeamCapacity)
            {
                bracket.Configure(
                    bracket.Type,
                    bracketSize,
                    bracket.IfNecessaryFinal,
                    bracket.DisplayName,
                    bracket.ThirdPlaceMatch);
            }

            await EntityGraphDeletes.ClearBracketSeedSlotsAsync(_db, bracket.Id, cancellationToken);

            var slotSeeds = BracketSeedOrder.BuildSlotSeedNumbers(bracketSize);

            for (var pos = 1; pos <= bracketSize; pos++)
            {
                var seedNumber = slotSeeds[pos - 1];
                Guid? tournamentTeamId = seedNumber <= orderedTournamentTeamIds.Count
                    ? orderedTournamentTeamIds[seedNumber - 1]
                    : null;

                var label = $"{bracket.DisplayName ?? "Bracket"}-{pos}";
                var slot = new BracketSeedSlot(
                    division.OrganizationId,
                    bracket.Id,
                    pos,
                    tournamentTeamId is null ? "bye" : "manual",
                    label);
                if (tournamentTeamId is { } ttId)
                    slot.SetManualOverride(ttId);
                slot.Resolve(tournamentTeamId);
                _db.BracketSeedSlots.Add(slot);
            }

            var hasNodes = await _db.BracketNodes.AnyAsync(n => n.BracketId == bracket.Id, cancellationToken);
            if (!hasNodes)
                await EnsureBracketNodesAsync(division.OrganizationId, bracket, cancellationToken);

            await EnsureAllBracketNodeMatchesAsync(division, bracketStage, bracket, cancellationToken);
            var slots = await _db.BracketSeedSlots
                .Where(s => s.BracketId == bracket.Id)
                .OrderBy(s => s.SeedPosition)
                .ToListAsync(cancellationToken);
            await ApplyResolvedTeamsToRoundOneAsync(bracket, slots, teamIdByTournamentTeamId, cancellationToken);
            await MatchSlotSourceSync.SyncBracketAdvancementSlotsAsync(
                _db, division.OrganizationId, bracket.Id, cancellationToken);
            await EnsureBracketAdvancementMatchesAndByesAsync(division, bracketStage, bracket, cancellationToken);
        }

        if (bracketStage.Status == StageStatus.Draft)
            bracketStage.TransitionTo(StageStatus.Configured);

        if (bracketStage.Status == StageStatus.Configured)
            bracketStage.TransitionTo(StageStatus.Seeded);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AdvancementRule? FindRuleForSlot(IReadOnlyList<AdvancementRule> rules, int seedPosition, int poolCount)
    {
        var direct = rules.FirstOrDefault(r => r.TargetSeedSlot == seedPosition && r.SourcePoolId is not null);
        if (direct is not null) return direct;

        return rules
            .Where(r => r.SourcePoolId is null
                        && seedPosition >= r.TargetSeedSlot
                        && seedPosition < r.TargetSeedSlot + poolCount)
            .OrderBy(r => r.Priority)
            .FirstOrDefault();
    }

    private static Dictionary<(Guid? poolId, int finish), List<(Guid poolId, Guid ttId)>> BuildWildcardBuckets(
        IReadOnlyList<PoolEntry> entries,
        IReadOnlyList<Pool> pools,
        IReadOnlyList<AdvancementRule> rules)
    {
        var result = new Dictionary<(Guid? poolId, int finish), List<(Guid poolId, Guid ttId)>>();
        var wildcardFinishes = rules.Where(r => r.SourcePoolId is null).Select(r => r.SourceFinishPosition).Distinct();
        foreach (var finish in wildcardFinishes)
        {
            var produced = new List<(Guid poolId, Guid ttId)>();
            foreach (var pool in pools.OrderBy(p => p.Sequence))
            {
                var entry = entries.FirstOrDefault(e => e.PoolId == pool.Id && e.FinishPosition == finish);
                if (entry is not null)
                    produced.Add((pool.Id, entry.TournamentTeamId));
            }
            result[(null, finish)] = produced;
        }
        return result;
    }

    private async Task EnsureBracketNodesAsync(Guid orgId, Bracket bracket, CancellationToken ct)
    {
        BracketGraphBuilder.AddNodes(_db, bracket, orgId);
        await Task.CompletedTask;
    }

    private async Task EnsureAllBracketNodeMatchesAsync(
        Division division,
        Stage bracketStage,
        Bracket bracket,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstAsync(t => t.Id == division.TournamentId, ct);
        var nodes = await _db.BracketNodes
            .Where(n => n.BracketId == bracket.Id)
            .OrderBy(n => n.Branch)
            .ThenBy(n => n.Round)
            .ThenBy(n => n.Slot)
            .ToListAsync(ct);

        var placeholderStart = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var duration = tournament.DefaultMatchDurationMinutes;
        var wave = await _db.Teams.Where(t => t.DivisionId == division.Id).Select(t => t.Wave).FirstOrDefaultAsync(ct);

        foreach (var node in nodes.Where(n => n.MatchId is null))
        {
            var match = new Match(
                division.OrganizationId,
                division.TournamentId,
                division.Id,
                wave,
                placeholderStart,
                duration,
                bracketNodeId: node.Id,
                stageId: bracketStage.Id);
            _db.Matches.Add(match);
            node.LinkMatch(match.Id);
        }
    }

    private async Task EnsureBracketRoundOneMatchesAsync(
        Division division,
        Stage bracketStage,
        Bracket bracket,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstAsync(t => t.Id == division.TournamentId, ct);
        var nodes = await _db.BracketNodes
            .Where(n => n.BracketId == bracket.Id && n.Branch == BracketBranch.Winners && n.Round == 1)
            .OrderBy(n => n.Slot)
            .ToListAsync(ct);

        var existingMatches = await _db.Matches
            .Where(m => m.DivisionId == division.Id && m.BracketNodeId != null && nodes.Select(n => n.Id).Contains(m.BracketNodeId.Value))
            .ToListAsync(ct);

        if (existingMatches.Count >= nodes.Count)
            return;

        var placeholderStart = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var duration = tournament.DefaultMatchDurationMinutes;
        var wave = await _db.Teams.Where(t => t.DivisionId == division.Id).Select(t => t.Wave).FirstOrDefaultAsync(ct);

        foreach (var node in nodes.Where(n => n.MatchId is null))
        {
            var match = new Match(
                division.OrganizationId,
                division.TournamentId,
                division.Id,
                wave,
                placeholderStart,
                duration,
                bracketNodeId: node.Id,
                stageId: bracketStage.Id);
            _db.Matches.Add(match);
            node.LinkMatch(match.Id);
        }
    }

    private async Task ApplyResolvedTeamsToRoundOneAsync(
        Bracket bracket,
        IReadOnlyList<BracketSeedSlot> slots,
        Dictionary<Guid, Guid> teamIdByTournamentTeamId,
        CancellationToken ct)
    {
        var nodes = await _db.BracketNodes
            .Where(n => n.BracketId == bracket.Id && n.Branch == BracketBranch.Winners && n.Round == 1)
            .OrderBy(n => n.Slot)
            .ToListAsync(ct);

        var resolvedTeams = slots
            .OrderBy(s => s.SeedPosition)
            .Select(s => s.ResolvedTournamentTeamId is { } tt
                ? teamIdByTournamentTeamId.GetValueOrDefault(tt)
                : (Guid?)null)
            .ToList();

        var idx = 0;
        var seed = 1;
        var ttByTeamId = teamIdByTournamentTeamId.ToDictionary(kv => kv.Value, kv => kv.Key);
        foreach (var node in nodes)
        {
            Guid? home = idx < resolvedTeams.Count ? resolvedTeams[idx] : null;
            idx++;
            Guid? away = idx < resolvedTeams.Count ? resolvedTeams[idx] : null;
            idx++;

            node.AssignTeam(1, home, home.HasValue ? seed++ : null);
            node.AssignTeam(2, away, away.HasValue ? seed++ : null);

            if (node.MatchId is { } matchId)
            {
                var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
                match?.AssignTeams(home, away);

                Guid? homeTt = home is { } h ? ttByTeamId.GetValueOrDefault(h) : null;
                Guid? awayTt = away is { } a ? ttByTeamId.GetValueOrDefault(a) : null;
                await MatchSlotSourceSync.EnsureFixedTeamSlotAsync(
                    _db, bracket.OrganizationId, matchId, MatchSlotKind.Home, homeTt, ct);
                await MatchSlotSourceSync.EnsureFixedTeamSlotAsync(
                    _db, bracket.OrganizationId, matchId, MatchSlotKind.Away, awayTt, ct);
            }
        }
    }

    private async Task EnsureBracketAdvancementMatchesAndByesAsync(
        Division division,
        Stage bracketStage,
        Bracket bracket,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstAsync(t => t.Id == division.TournamentId, ct);
        var nodes = await _db.BracketNodes
            .Where(n => n.BracketId == bracket.Id)
            .OrderBy(n => n.Branch)
            .ThenBy(n => n.Round)
            .ThenBy(n => n.Slot)
            .ToListAsync(ct);
        var byId = nodes.ToDictionary(n => n.Id);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in nodes)
            {
                var winnerByBye = node.HomeTeamId ?? node.AwayTeamId;
                if (winnerByBye is null || (node.HomeTeamId is not null && node.AwayTeamId is not null))
                    continue;

                if (node.WinsToNodeId is not { } winsTo || !byId.TryGetValue(winsTo, out var next))
                    continue;

                var targetSlot = node.WinsToSlot ?? 1;
                var alreadyAssigned = targetSlot == 1 ? next.HomeTeamId : next.AwayTeamId;
                if (alreadyAssigned == winnerByBye)
                    continue;

                next.AssignTeam(targetSlot, winnerByBye);
                changed = true;
            }
        }

        var placeholderStart = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var duration = tournament.DefaultMatchDurationMinutes;
        var defaultWave = await _db.Teams.Where(t => t.DivisionId == division.Id).Select(t => t.Wave).FirstOrDefaultAsync(ct);

        foreach (var node in nodes.Where(n => n.HomeTeamId is not null && n.AwayTeamId is not null))
        {
            if (node.MatchId is { } existingMatchId)
            {
                var existing = await _db.Matches.FirstOrDefaultAsync(m => m.Id == existingMatchId, ct);
                existing?.AssignTeams(node.HomeTeamId, node.AwayTeamId);
                continue;
            }

            var match = new Match(
                division.OrganizationId,
                division.TournamentId,
                division.Id,
                defaultWave,
                placeholderStart,
                duration,
                bracketNodeId: node.Id,
                homeTeamId: node.HomeTeamId,
                awayTeamId: node.AwayTeamId,
                stageId: bracketStage.Id);
            _db.Matches.Add(match);
            node.LinkMatch(match.Id);
        }
    }

    private async Task<List<Pool>> EnsurePoolsAsync(Division division, int poolCount, CancellationToken ct)
    {
        var pools = await _db.Pools
            .Where(p => p.DivisionId == division.Id)
            .OrderBy(p => p.Sequence)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        if (pools.Count >= poolCount)
            return pools.Take(poolCount).ToList();

        var divisionPools = new List<Pool>(poolCount);
        for (var i = 0; i < poolCount; i++)
        {
            Pool? pool = i < pools.Count ? pools[i] : null;
            if (pool is null)
            {
                var name = $"Pool {(char)('A' + i)}";
                pool = pools.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            if (pool is null)
            {
                var name = $"Pool {(char)('A' + i)}";
                pool = new Pool(division.OrganizationId, division.Id, name, i, targetSize: 4);
                _db.Pools.Add(pool);
                pools.Add(pool);
            }

            divisionPools.Add(pool);
        }

        return divisionPools;
    }

    private async Task BackfillDivisionRankingsAsync(Division division, List<Team> teams, CancellationToken ct)
    {
        var rankByTeam = new Dictionary<Guid, int>(
            await DivisionEntryRankingService.GetRankByTeamIdAsync(_db, division.Id, ct));

        var missingRanking = teams
            .Where(t => !rankByTeam.ContainsKey(t.Id))
            .OrderBy(t => t.SeedHint ?? int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingRanking.Count == 0)
            return;

        var appendMode = rankByTeam.Count > 0;
        var maxExisting = rankByTeam.Values.DefaultIfEmpty(0).Max();
        var next = Math.Max(maxExisting, 0) + 1;

        foreach (var team in missingRanking)
        {
            var tt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);
            await DivisionEntryRankingService.UpsertRankingAsync(
                _db, division, tt, next, source: appendMode ? "append_legacy" : "seed_legacy", isManualOverride: false, ct);
            appendMode = true;
            next++;
        }
    }

    private async Task ReplaceDivisionPoolMatchesAsync(
        Division division,
        Stage poolStage,
        PoolSettings? poolSettings,
        CancellationToken ct)
    {
        var refAssignmentMode = poolSettings?.RefAssignmentMode ?? "next_in_pool";
        var tournament = await _db.Tournaments.FirstAsync(t => t.Id == division.TournamentId, ct);
        var scheduleVersion = await ScheduleVersionProvisioning.EnsureCurrentAsync(_db, division.TournamentId, ct);

        await EntityGraphDeletes.ClearDivisionPoolPlayMatchesAsync(
            _db, division.Id, poolStage.Id, scheduleVersion.Id, ct);

        var pools = await _db.Pools.Where(p => p.DivisionId == division.Id).ToListAsync(ct);
        var teams = await _db.Teams.Where(t => t.DivisionId == division.Id).ToListAsync(ct);
        var placeholderStart = tournament.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var duration = tournament.DefaultMatchDurationMinutes;

        var tournamentTeamByTeamId = await (
            from tt in _db.TournamentTeams
            where tt.TournamentId == division.TournamentId && tt.DivisionId == division.Id
            select new { tt.TeamId, tt.Id }).ToDictionaryAsync(x => x.TeamId, x => x.Id, ct);

        foreach (var pool in pools)
        {
            var poolTeams = teams.Where(t => t.PoolId == pool.Id)
                .OrderBy(t => t.SeedHint ?? int.MaxValue)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => t.Id)
                .ToList();
            if (poolTeams.Count < 2)
                continue;

            var wave = teams.First(t => t.PoolId == pool.Id).Wave;
            var matchIndex = 0;
            foreach (var (homeId, awayId) in BuildRoundRobin(poolTeams))
            {
                Guid? workTeamId = null;
                if (string.Equals(refAssignmentMode, "next_in_pool", StringComparison.OrdinalIgnoreCase))
                    workTeamId = WorkTeamAutoRotation.PickWorkTeamTeamId(poolTeams, homeId, awayId, matchIndex);

                var match = new Match(
                    division.OrganizationId,
                    division.TournamentId,
                    division.Id,
                    wave,
                    placeholderStart,
                    duration,
                    poolId: pool.Id,
                    homeTeamId: homeId,
                    awayTeamId: awayId,
                    refTeamId: workTeamId,
                    stageId: poolStage.Id);
                _db.Matches.Add(match);

                if (tournamentTeamByTeamId.TryGetValue(homeId, out var homeTt))
                    await MatchSlotSourceSync.EnsureFixedTeamSlotAsync(
                        _db, division.OrganizationId, match.Id, MatchSlotKind.Home, homeTt, ct);
                if (tournamentTeamByTeamId.TryGetValue(awayId, out var awayTt))
                    await MatchSlotSourceSync.EnsureFixedTeamSlotAsync(
                        _db, division.OrganizationId, match.Id, MatchSlotKind.Away, awayTt, ct);
                if (workTeamId is not null
                    && tournamentTeamByTeamId.TryGetValue(workTeamId.Value, out var workTournamentTeamId))
                {
                    await MatchSlotSourceSync.EnsureFixedTeamSlotAsync(
                        _db, division.OrganizationId, match.Id, MatchSlotKind.Ref, workTournamentTeamId, ct);
                    _db.MatchOfficials.Add(new MatchOfficials(
                        division.OrganizationId,
                        match.Id,
                        workTournamentTeamId));
                }

                matchIndex++;
            }
        }
    }

    private static List<(Guid HomeId, Guid AwayId)> BuildRoundRobin(List<Guid> teams)
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

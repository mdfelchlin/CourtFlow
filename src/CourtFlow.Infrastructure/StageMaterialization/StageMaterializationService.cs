using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Persistence;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.Features.Teams;
using CourtFlow.Application.Seeding;
using CourtFlow.Application.Standings;
using CourtFlow.Application.StageMaterialization;
using CourtFlow.Application.WorkTeams;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
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

        await ReplaceDivisionPoolMatchesAsync(division, poolStage, cancellationToken);
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

            await EnsureBracketRoundOneMatchesAsync(division, bracketStage, bracket, cancellationToken);
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
        }

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
        var size = NextPowerOfTwo(bracket.TeamCapacity);
        var winners = new Dictionary<(int round, int slot), BracketNode>();
        var totalRounds = (int)Math.Log2(size);

        for (var round = 1; round <= totalRounds; round++)
        {
            var slotsInRound = size >> round;
            for (var slot = 0; slot < slotsInRound; slot++)
            {
                var node = new BracketNode(orgId, bracket.Id, BracketBranch.Winners, round, slot);
                bracket.AddNode(node);
                _db.BracketNodes.Add(node);
                winners[(round, slot)] = node;
            }
        }

        for (var round = 1; round < totalRounds; round++)
        {
            var slotsInRound = size >> round;
            for (var slot = 0; slot < slotsInRound; slot++)
            {
                var parentSlot = slot / 2;
                winners[(round, slot)].WireAdvancement(
                    winners[(round + 1, parentSlot)].Id,
                    slot % 2 == 0 ? 1 : 2,
                    null,
                    null);
            }
        }

        if (bracket.Type == BracketType.DoubleElimination)
        {
            var losersRounds = totalRounds * 2 - 1;
            for (var round = 1; round <= losersRounds; round++)
            {
                var slotsInRound = Math.Max(1, size >> ((round + 1) / 2 + 1));
                for (var slot = 0; slot < slotsInRound; slot++)
                {
                    var node = new BracketNode(orgId, bracket.Id, BracketBranch.Losers, round, slot);
                    bracket.AddNode(node);
                    _db.BracketNodes.Add(node);
                }
            }

            var grandFinal = new BracketNode(orgId, bracket.Id, BracketBranch.GrandFinal, 1, 0);
            bracket.AddNode(grandFinal);
            _db.BracketNodes.Add(grandFinal);

            if (bracket.IfNecessaryFinal)
            {
                var ifNecessary = new BracketNode(orgId, bracket.Id, BracketBranch.GrandFinal, 2, 0);
                bracket.AddNode(ifNecessary);
                _db.BracketNodes.Add(ifNecessary);
                grandFinal.WireAdvancement(ifNecessary.Id, 1, ifNecessary.Id, 2);
            }
        }

        await Task.CompletedTask;
    }

    private static int NextPowerOfTwo(int n)
    {
        var p = 1;
        while (p < n) p <<= 1;
        return p;
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
            }
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

    private async Task ReplaceDivisionPoolMatchesAsync(Division division, Stage poolStage, CancellationToken ct)
    {
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
                var workTeamId = WorkTeamAutoRotation.PickWorkTeamTeamId(poolTeams, homeId, awayId, matchIndex);
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

                if (workTeamId is not null
                    && tournamentTeamByTeamId.TryGetValue(workTeamId.Value, out var workTournamentTeamId))
                {
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

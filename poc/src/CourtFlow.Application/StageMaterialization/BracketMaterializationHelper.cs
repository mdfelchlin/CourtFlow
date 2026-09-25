using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.Standings;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.StageMaterialization;

internal static class BracketMaterializationHelper
{
    public static async Task<(Stage poolStage, Stage bracketStage)> RequireStagesAsync(
        IAppDbContext db, Guid divisionId, CancellationToken ct)
    {
        var stages = await db.Stages
            .Where(s => s.DivisionId == divisionId)
            .OrderBy(s => s.StageNumber)
            .ToListAsync(ct);

        var poolStage = stages.FirstOrDefault(s => s.StageType == StageType.Pool)
            ?? throw new InvalidOperationException("Pool stage not found.");
        var bracketStage = stages.FirstOrDefault(s => s.StageType == StageType.Bracket)
            ?? throw new InvalidOperationException("Bracket stage not found.");

        return (poolStage, bracketStage);
    }

    public static async Task<Stage> RequireBracketStageAsync(IAppDbContext db, Guid divisionId, CancellationToken ct)
    {
        var bracketStage = await db.Stages
            .Where(s => s.DivisionId == divisionId && s.StageType == StageType.Bracket)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Bracket stage not found.");

        return bracketStage;
    }

    public static async Task<Dictionary<Guid, Guid>> TeamIdByTournamentTeamIdAsync(
        IAppDbContext db, Guid divisionId, CancellationToken ct)
    {
        return await db.TournamentTeams
            .Where(tt => tt.DivisionId == divisionId)
            .ToDictionaryAsync(tt => tt.Id, tt => tt.TeamId, ct);
    }

    public static async Task SyncFinishPositionsFromStandingsAsync(
        IAppDbContext db,
        IStandingsCalculator standings,
        Guid divisionId,
        CancellationToken ct)
    {
        var standingResult = await standings.ForDivisionAsync(divisionId, ct);
        var standingRows = standingResult.Pools;
        var poolIds = standingRows.Select(p => p.PoolId).ToHashSet();
        var entries = await db.PoolEntries.Where(e => poolIds.Contains(e.PoolId)).ToListAsync(ct);
        var teamById = await db.Teams.Where(t => t.DivisionId == divisionId)
            .ToDictionaryAsync(t => t.Id, t => t, ct);
        var ttByTeamId = await db.TournamentTeams.Where(tt => tt.DivisionId == divisionId)
            .ToDictionaryAsync(tt => tt.TeamId, tt => tt.Id, ct);

        foreach (var poolStanding in standingRows)
        {
            foreach (var row in poolStanding.Rows)
            {
                if (!ttByTeamId.TryGetValue(row.TeamId, out var ttId))
                    continue;

                var entry = entries.FirstOrDefault(e =>
                    e.PoolId == poolStanding.PoolId && e.TournamentTeamId == ttId);
                entry?.SetFinishPosition(row.Rank);
            }
        }
    }

    public static Guid? ResolveSlotTournamentTeamId(
        BracketSeedSlot slot,
        IReadOnlyList<AdvancementRule> rules,
        IReadOnlyList<Pool> pools,
        Dictionary<(Guid poolId, int finish), Guid> finishByPool,
        Dictionary<(Guid? poolId, int finish), List<(Guid poolId, Guid ttId)>> wildcardBuckets)
    {
        if (slot.ManualTournamentTeamId is { } manual)
            return manual;

        if (slot.ExpectedSourceType == "bye")
            return null;

        if (slot.AdvancementRuleId is not { } ruleId)
            return null;

        var rule = rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null)
            return null;

        if (rule.SourcePoolId is { } poolId)
        {
            return finishByPool.TryGetValue((poolId, rule.SourceFinishPosition), out var ttId)
                ? ttId
                : null;
        }

        var key = ((Guid?)null, rule.SourceFinishPosition);
        if (!wildcardBuckets.TryGetValue(key, out var produced))
            return null;

        var ordered = rule.OrderBy == "tiebreaker_rank"
            ? produced.OrderBy(x => x.poolId).ToList()
            : produced.OrderBy(x => pools.FirstOrDefault(p => p.Id == x.poolId)?.Sequence ?? int.MaxValue).ToList();

        var slotOffset = slot.SeedPosition - rule.TargetSeedSlot;
        if (slotOffset < 0 || slotOffset >= ordered.Count)
            return null;

        return ordered[slotOffset].ttId;
    }
}

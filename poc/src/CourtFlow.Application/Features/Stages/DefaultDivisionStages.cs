using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Stages;

public static class DefaultDivisionStages
{
    /// <summary>Idempotent stage rows for a new division, based on play format.</summary>
    public static async Task EnsureProvisionedAsync(IAppDbContext db, Division division, CancellationToken ct = default)
    {
        if (await db.Stages.AsNoTracking().AnyAsync(s => s.DivisionId == division.Id, ct))
            return;

        ProvisionStages(db, division);
    }

    /// <summary>Adds missing stages when play format changes; never removes existing stages.</summary>
    public static async Task EnsureMissingStagesForFormatAsync(IAppDbContext db, Division division, CancellationToken ct = default)
    {
        var existing = await db.Stages.Where(s => s.DivisionId == division.Id).ToListAsync(ct);
        if (existing.Count == 0)
        {
            ProvisionStages(db, division);
            return;
        }

        var hasPool = existing.Any(s => s.StageType == StageType.Pool);
        var hasBracket = existing.Any(s => s.StageType == StageType.Bracket);
        var usedNumbers = existing.Select(s => s.StageNumber).ToHashSet();

        switch (division.Format)
        {
            case DivisionFormat.PoolOnly when !hasPool:
                AddPoolStage(db, division, AllocateStageNumber(usedNumbers, preferred: 1));
                break;
            case DivisionFormat.BracketOnly when !hasBracket:
                AddBracketStage(db, division, AllocateStageNumber(usedNumbers, preferred: 1));
                break;
            case DivisionFormat.PoolPlusBracket:
                if (!hasPool)
                    AddPoolStage(db, division, AllocateStageNumber(usedNumbers, preferred: 1));
                if (!hasBracket)
                    AddBracketStage(db, division, AllocateStageNumber(usedNumbers, preferred: 2));
                break;
        }
    }

    private static int AllocateStageNumber(HashSet<int> usedNumbers, int preferred)
    {
        if (!usedNumbers.Contains(preferred))
        {
            usedNumbers.Add(preferred);
            return preferred;
        }

        var n = 1;
        while (usedNumbers.Contains(n))
            n++;
        usedNumbers.Add(n);
        return n;
    }

    private static void ProvisionStages(IAppDbContext db, Division division)
    {
        switch (division.Format)
        {
            case DivisionFormat.PoolOnly:
                AddPoolStage(db, division, 1);
                break;
            case DivisionFormat.BracketOnly:
                AddBracketStage(db, division, 1);
                break;
            default:
                AddPoolStage(db, division, 1);
                AddBracketStage(db, division, 2);
                break;
        }
    }

    private static void AddPoolStage(IAppDbContext db, Division division, int stageNumber)
    {
        var poolStage = new Stage(division.OrganizationId, division.Id, stageNumber, StageType.Pool, "Pool play");
        db.Stages.Add(poolStage);
        db.PoolSettings.Add(new PoolSettings(division.OrganizationId, poolStage.Id));
    }

    private static void AddBracketStage(IAppDbContext db, Division division, int stageNumber)
    {
        db.Stages.Add(new Stage(division.OrganizationId, division.Id, stageNumber, StageType.Bracket, "Bracket"));
    }
}

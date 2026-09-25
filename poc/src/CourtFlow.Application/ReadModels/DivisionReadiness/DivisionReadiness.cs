using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.ReadModels.DivisionReadiness;

public sealed record DivisionReadinessDto(
    bool CanEditRankings,
    bool CanEditPoolStructure,
    bool CanSnakeIntoPools,
    bool CanEditBracketStructure,
    bool CanGenerateBrackets,
    bool CanResolveBracketSlots,
    IReadOnlyList<string> BlockingReasons);

public interface IDivisionReadiness
{
    Task<DivisionReadinessDto> EvaluateAsync(Guid divisionId, CancellationToken ct = default);
}

/// <summary>Coarse division gates derived from fine-grained stage status (ADR-0006).</summary>
public sealed class DivisionReadinessService : IDivisionReadiness
{
    private readonly IAppDbContext _db;

    public DivisionReadinessService(IAppDbContext db) => _db = db;

    public async Task<DivisionReadinessDto> EvaluateAsync(Guid divisionId, CancellationToken ct = default)
    {
        var stages = await _db.Stages.AsNoTracking()
            .Where(s => s.DivisionId == divisionId)
            .OrderBy(s => s.StageNumber)
            .ToListAsync(ct);

        var poolStage = stages.FirstOrDefault(s => s.StageType == StageType.Pool);
        var bracketStage = stages.FirstOrDefault(s => s.StageType == StageType.Bracket);

        var rankingsLocked = stages.Any(s => s.Status is StageStatus.InProgress or StageStatus.Completed);
        var poolLocked = poolStage?.Status is StageStatus.InProgress or StageStatus.Completed;
        var bracketLocked = bracketStage?.Status is StageStatus.InProgress or StageStatus.Completed;

        var teamCount = await _db.Teams.CountAsync(t => t.DivisionId == divisionId, ct);
        var poolCount = await _db.Pools.CountAsync(p => p.DivisionId == divisionId, ct);

        var blocking = new List<string>();
        if (rankingsLocked)
            blocking.Add("Rankings are locked while a stage is in progress.");
        if (poolLocked)
            blocking.Add("Pool structure is locked while pool play is in progress.");
        if (bracketLocked)
            blocking.Add("Bracket structure is locked while bracket play is in progress.");

        var canSnake = !rankingsLocked && teamCount >= 3 && poolCount > 0;
        if (!canSnake && !rankingsLocked && teamCount < 3)
            blocking.Add("At least 3 teams required to snake into pools.");

        return new DivisionReadinessDto(
            CanEditRankings: !rankingsLocked,
            CanEditPoolStructure: !poolLocked,
            CanSnakeIntoPools: canSnake,
            CanEditBracketStructure: !bracketLocked,
            CanGenerateBrackets: !bracketLocked,
            CanResolveBracketSlots: !bracketLocked && poolStage?.Status is StageStatus.Seeded or StageStatus.InProgress or StageStatus.Completed,
            BlockingReasons: blocking);
    }
}

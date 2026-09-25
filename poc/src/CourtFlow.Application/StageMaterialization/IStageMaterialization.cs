namespace CourtFlow.Application.StageMaterialization;

/// <summary>Pool + bracket materialization seam per ADR-0005.</summary>
public interface IStageMaterialization
{
    Task SnakeIntoPoolsAsync(Guid divisionId, CancellationToken cancellationToken = default);
    Task GeneratePoolMatchesAsync(Guid divisionId, CancellationToken cancellationToken = default);
    Task GenerateBracketsAsync(Guid divisionId, CancellationToken cancellationToken = default);
    Task ResolveBracketSlotsAsync(Guid divisionId, CancellationToken cancellationToken = default);
    Task SeedBracketFromRankingsAsync(Guid divisionId, CancellationToken cancellationToken = default);
}

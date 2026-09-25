namespace CourtFlow.Application.Brackets;

public interface IBracketProgressionService
{
    Task AdvanceAsync(Guid matchId, CancellationToken ct = default);
}

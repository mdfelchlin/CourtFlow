namespace CourtFlow.Application.Matches;

public interface IMatchProgressionService
{
    Task OnMatchFinalizedAsync(Guid matchId, CancellationToken ct = default);
    Task ResolveStandingsSourcesAsync(Guid divisionId, CancellationToken ct = default);
}

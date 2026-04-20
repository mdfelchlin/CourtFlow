using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Brackets;

public sealed record BracketBuildOptions(BracketType Type, int TeamCapacity, bool IfNecessaryFinal);

public interface IBracketBuilder
{
    Task<Bracket> BuildAsync(Guid tournamentId, Guid divisionId, BracketBuildOptions options, CancellationToken ct = default);
}

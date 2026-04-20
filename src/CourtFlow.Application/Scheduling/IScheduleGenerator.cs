using CourtFlow.Domain.Entities;

namespace CourtFlow.Application.Scheduling;

public interface IScheduleGenerator
{
    Task<IReadOnlyCollection<Match>> GeneratePoolPlayAsync(Guid tournamentId, CancellationToken ct = default);
}

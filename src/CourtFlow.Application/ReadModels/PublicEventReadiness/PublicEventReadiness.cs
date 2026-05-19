using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.ReadModels.PublicEventReadiness;

public sealed record PublicEventReadinessDto(
    bool TeamsReady,
    bool ScheduleReady,
    bool BracketsReady,
    bool StandingsReady);

public interface IPublicEventReadiness
{
    Task<PublicEventReadinessDto> EvaluateAsync(Guid tournamentId, CancellationToken ct = default);
}

public sealed class PublicEventReadinessService : IPublicEventReadiness
{
    private readonly IAppDbContext _db;

    public PublicEventReadinessService(IAppDbContext db) => _db = db;

    public async Task<PublicEventReadinessDto> EvaluateAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var teamCount = await _db.TournamentTeams
            .IgnoreQueryFilters()
            .CountAsync(t => t.TournamentId == tournamentId, ct);

        var scheduleReady = await _db.ScheduleVersions
            .IgnoreQueryFilters()
            .AnyAsync(v => v.TournamentId == tournamentId && v.Status == ScheduleVersionStatus.Published, ct);

        var bracketsReady = await (
            from s in _db.BracketSeedSlots.IgnoreQueryFilters()
            join b in _db.Brackets.IgnoreQueryFilters() on s.BracketId equals b.Id
            where b.TournamentId == tournamentId
            select s.Id).AnyAsync(ct);

        var standingsReady = await _db.Matches
            .IgnoreQueryFilters()
            .AnyAsync(m => m.TournamentId == tournamentId && m.PoolId != null && m.State == MatchState.Final, ct);

        return new PublicEventReadinessDto(
            TeamsReady: teamCount > 0,
            ScheduleReady: scheduleReady,
            BracketsReady: bracketsReady,
            StandingsReady: standingsReady);
    }
}

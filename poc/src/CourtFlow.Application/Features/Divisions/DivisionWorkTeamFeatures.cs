using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Divisions;

public sealed record PoolMatchWorkTeamDto(
    int Order,
    Guid MatchId,
    string HomeTeamName,
    string AwayTeamName,
    string? HomeTeamDisplay,
    string? AwayTeamDisplay,
    Guid? WorkTeamTournamentTeamId,
    string? WorkTeamName,
    string? WorkTeamDisplay,
    string AssignmentSource);

public sealed record DivisionPoolWorkRotationDto(
    Guid PoolId,
    string PoolName,
    IReadOnlyList<PoolMatchWorkTeamDto> Matches);

public sealed record GetDivisionWorkRotationQuery(Guid DivisionId) : IRequest<IReadOnlyList<DivisionPoolWorkRotationDto>?>;

public sealed class GetDivisionWorkRotationHandler : IRequestHandler<GetDivisionWorkRotationQuery, IReadOnlyList<DivisionPoolWorkRotationDto>?>
{
    private readonly IAppDbContext _db;

    public GetDivisionWorkRotationHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DivisionPoolWorkRotationDto>?> Handle(GetDivisionWorkRotationQuery request, CancellationToken ct)
    {
        var division = await _db.Divisions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var poolStage = await _db.Stages.AsNoTracking()
            .Where(s => s.DivisionId == division.Id && s.StageType == StageType.Pool)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);

        if (poolStage is null || poolStage.Status is StageStatus.Draft or StageStatus.Configured)
            return Array.Empty<DivisionPoolWorkRotationDto>();

        var pools = await _db.Pools.AsNoTracking()
            .Where(p => p.DivisionId == division.Id)
            .OrderBy(p => p.Sequence).ThenBy(p => p.Name)
            .ToListAsync(ct);

        var matches = await _db.Matches.AsNoTracking()
            .Where(m => m.DivisionId == division.Id && m.PoolId != null && m.StageId == poolStage.Id)
            .ToListAsync(ct);

        var teamIds = matches
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId, m.RefTeamId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var teams = await _db.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        static string Display(Team t) =>
            string.IsNullOrWhiteSpace(t.Club) ? t.Name : $"{t.Club.Trim()} {t.Name.Trim()}";

        var matchIds = matches.Select(m => m.Id).ToList();
        var officials = await _db.MatchOfficials.AsNoTracking()
            .Where(o => matchIds.Contains(o.MatchId))
            .ToDictionaryAsync(o => o.MatchId, ct);

        var ttIds = officials.Values
            .Where(o => o.WorkTeamTournamentTeamId.HasValue)
            .Select(o => o.WorkTeamTournamentTeamId!.Value)
            .Distinct()
            .ToList();
        var ttNames = ttIds.Count == 0
            ? new Dictionary<Guid, (string Name, string? Club)>()
            : await (
                from tt in _db.TournamentTeams.AsNoTracking()
                join t in _db.Teams.AsNoTracking() on tt.TeamId equals t.Id
                where ttIds.Contains(tt.Id)
                select new { tt.Id, t.Name, t.Club }).ToDictionaryAsync(x => x.Id, x => (x.Name, x.Club), ct);

        return pools.Select(pool =>
        {
            var poolMatches = matches.Where(m => m.PoolId == pool.Id)
                .OrderBy(m => m.ScheduledStartUtc).ThenBy(m => m.Id)
                .ToList();
            var rows = poolMatches.Select((m, idx) =>
            {
                officials.TryGetValue(m.Id, out var official);
                var workTtId = official?.WorkTeamTournamentTeamId;
                var source = official?.AssignmentSource.ToString() ?? "None";
                teams.TryGetValue(m.HomeTeamId ?? Guid.Empty, out var home);
                teams.TryGetValue(m.AwayTeamId ?? Guid.Empty, out var away);
                var homeName = home?.Name ?? "TBD";
                var awayName = away?.Name ?? "TBD";
                string? workName = null;
                string? workDisplay = null;
                if (workTtId is { } wId && ttNames.TryGetValue(wId, out var workInfo))
                {
                    workName = workInfo.Name;
                    workDisplay = string.IsNullOrWhiteSpace(workInfo.Club)
                        ? workInfo.Name
                        : $"{workInfo.Club.Trim()} {workInfo.Name.Trim()}";
                }

                return new PoolMatchWorkTeamDto(
                    idx + 1,
                    m.Id,
                    homeName,
                    awayName,
                    home is null ? null : Display(home),
                    away is null ? null : Display(away),
                    workTtId,
                    workName,
                    workDisplay,
                    source);
            }).ToList();
            return new DivisionPoolWorkRotationDto(pool.Id, pool.Name, rows);
        }).ToList();
    }
}

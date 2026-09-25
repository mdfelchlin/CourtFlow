using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

internal static class TournamentQueryHelper
{
    public static async Task<DivisionFormat?> ResolvePlayFormatAsync(
        IAppDbContext db,
        Guid tournamentId,
        CancellationToken ct = default)
    {
        var formats = await db.Divisions.AsNoTracking()
            .Where(d => d.TournamentId == tournamentId)
            .Select(d => d.Format)
            .Distinct()
            .ToListAsync(ct);
        return formats.Count == 1 ? formats[0] : null;
    }

    public static async Task<IReadOnlyDictionary<Guid, DivisionFormat?>> ResolvePlayFormatsAsync(
        IAppDbContext db,
        IReadOnlyCollection<Guid> tournamentIds,
        CancellationToken ct = default)
    {
        if (tournamentIds.Count == 0)
            return new Dictionary<Guid, DivisionFormat?>();

        var rows = await db.Divisions.AsNoTracking()
            .Where(d => tournamentIds.Contains(d.TournamentId))
            .Select(d => new { d.TournamentId, d.Format })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.TournamentId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var formats = g.Select(x => x.Format).Distinct().ToList();
                    return formats.Count == 1 ? formats[0] : (DivisionFormat?)null;
                });
    }

    public static async Task<TournamentDto> ToDtoAsync(this Tournament t, IAppDbContext db, CancellationToken ct = default)
    {
        var playFormat = await ResolvePlayFormatAsync(db, t.Id, ct);
        return t.ToDto(playFormat);
    }
}

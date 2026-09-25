using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Venues;

internal static class VenueCourtSync
{
    public static async Task<int> SyncAsync(
        IAppDbContext db,
        Guid organizationId,
        Guid venueId,
        int targetCount,
        CancellationToken ct)
    {
        if (targetCount < 0)
            throw new ValidationException("Court count cannot be negative.");

        var courts = await db.Courts
            .Where(c => c.VenueId == venueId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        while (courts.Count > targetCount)
        {
            var toRemove = SelectCourtToRemove(courts);
            await EnsureCourtCanBeRemovedAsync(db, toRemove, ct);
            db.Courts.Remove(toRemove);
            courts.Remove(toRemove);
        }

        while (courts.Count < targetCount)
        {
            var name = NextCourtName(courts);
            var court = new Court(organizationId, venueId, name);
            db.Courts.Add(court);
            courts.Add(court);
        }

        return courts.Count;
    }

    private static string NextCourtName(IReadOnlyList<Court> courts)
    {
        var names = courts.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var n = 1; n <= courts.Count + 1000; n++)
        {
            var candidate = n.ToString();
            if (!names.Contains(candidate))
                return candidate;
        }

        throw new ValidationException("Unable to assign a court number.");
    }

    private static Court SelectCourtToRemove(IReadOnlyList<Court> courts) =>
        courts
            .OrderByDescending(CourtDeleteSortKey)
            .ThenByDescending(c => c.CreatedAtUtc)
            .First();

    private static int CourtDeleteSortKey(Court court) =>
        int.TryParse(court.Name, out var n) ? n : int.MinValue;

    private static async Task EnsureCourtCanBeRemovedAsync(IAppDbContext db, Court court, CancellationToken ct)
    {
        var blocked = await db.Matches
            .Where(m => m.CourtId == court.Id
                        && (m.State == MatchState.NotStarted || m.State == MatchState.InProgress))
            .Select(m => m.State)
            .AnyAsync(ct);

        if (blocked)
            throw new ValidationException(
                $"Cannot remove court '{court.Name}' because it has scheduled or in-progress matches.");
    }
}

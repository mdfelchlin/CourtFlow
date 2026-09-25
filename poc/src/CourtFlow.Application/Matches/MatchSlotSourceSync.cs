using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Matches;

public static class MatchSlotSourceSync
{
    public static async Task EnsureFixedTeamSlotAsync(
        IAppDbContext db,
        Guid organizationId,
        Guid matchId,
        MatchSlotKind slot,
        Guid? tournamentTeamId,
        CancellationToken ct = default)
    {
        var existing = await db.MatchSlotSources
            .FirstOrDefaultAsync(s => s.MatchId == matchId && s.Slot == slot, ct);

        if (tournamentTeamId is null)
        {
            if (existing is not null)
                db.MatchSlotSources.Remove(existing);
            return;
        }

        if (existing is null)
        {
            existing = new MatchSlotSource(organizationId, matchId, slot, MatchSlotSourceKind.FixedTeam);
            db.MatchSlotSources.Add(existing);
        }

        existing.ConfigureFixedTeam(tournamentTeamId.Value);
    }

    public static async Task EnsureMatchOutcomeSlotAsync(
        IAppDbContext db,
        Guid organizationId,
        Guid matchId,
        MatchSlotKind slot,
        Guid sourceMatchId,
        MatchSlotSourceKind kind,
        CancellationToken ct = default)
    {
        if (kind is not (MatchSlotSourceKind.MatchWinner or MatchSlotSourceKind.MatchLoser))
            throw new ArgumentException("Kind must be MatchWinner or MatchLoser.", nameof(kind));

        var existing = await db.MatchSlotSources
            .FirstOrDefaultAsync(s => s.MatchId == matchId && s.Slot == slot, ct);

        if (existing is null)
        {
            existing = new MatchSlotSource(organizationId, matchId, slot, kind);
            db.MatchSlotSources.Add(existing);
        }

        existing.ConfigureMatchOutcome(sourceMatchId, kind);
    }

    public static async Task SyncBracketAdvancementSlotsAsync(
        IAppDbContext db,
        Guid organizationId,
        Guid bracketId,
        CancellationToken ct = default)
    {
        var nodes = await db.BracketNodes
            .Where(n => n.BracketId == bracketId && n.MatchId != null)
            .ToListAsync(ct);
        var byId = nodes.ToDictionary(n => n.Id);

        foreach (var node in nodes)
        {
            if (node.MatchId is not { } sourceMatchId)
                continue;

            if (node.WinsToNodeId is { } winsToId
                && byId.TryGetValue(winsToId, out var winsTo)
                && winsTo.MatchId is { } winTargetMatchId
                && node.WinsToSlot is { } winSlotNum)
            {
                var slot = winSlotNum == 1 ? MatchSlotKind.Home : MatchSlotKind.Away;
                await EnsureMatchOutcomeSlotAsync(
                    db, organizationId, winTargetMatchId, slot, sourceMatchId,
                    MatchSlotSourceKind.MatchWinner, ct);
            }

            if (node.LosesToNodeId is { } losesToId
                && byId.TryGetValue(losesToId, out var losesTo)
                && losesTo.MatchId is { } loseTargetMatchId
                && node.LosesToSlot is { } loseSlotNum)
            {
                var slot = loseSlotNum == 1 ? MatchSlotKind.Home : MatchSlotKind.Away;
                await EnsureMatchOutcomeSlotAsync(
                    db, organizationId, loseTargetMatchId, slot, sourceMatchId,
                    MatchSlotSourceKind.MatchLoser, ct);
            }
        }
    }
}

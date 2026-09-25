using CourtFlow.Application.Brackets;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Brackets;

public class BracketProgressionService : IBracketProgressionService
{
    private readonly AppDbContext _db;

    public BracketProgressionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task AdvanceAsync(Guid matchId, CancellationToken ct = default)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null || match.State != MatchState.Final || match.BracketNodeId is null) return;

        var node = await _db.BracketNodes.FirstOrDefaultAsync(n => n.Id == match.BracketNodeId, ct);
        if (node is null) return;

        var winnerId = match.WinnerTeamId;
        var loserId = (winnerId == match.HomeTeamId) ? match.AwayTeamId : match.HomeTeamId;

        if (node.WinsToNodeId is { } winsTo && winnerId is { })
        {
            var next = await _db.BracketNodes.FirstOrDefaultAsync(n => n.Id == winsTo, ct);
            if (next is not null)
            {
                next.AssignTeam(node.WinsToSlot ?? 1, winnerId);
                await EnsureMatchForReadyNodeAsync(next, match, ct);
            }
        }
        if (node.LosesToNodeId is { } losesTo && loserId is { })
        {
            var next = await _db.BracketNodes.FirstOrDefaultAsync(n => n.Id == losesTo, ct);
            if (next is not null)
            {
                next.AssignTeam(node.LosesToSlot ?? 2, loserId);
                await EnsureMatchForReadyNodeAsync(next, match, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureMatchForReadyNodeAsync(Domain.Entities.BracketNode node, Domain.Entities.Match sourceMatch, CancellationToken ct)
    {
        if (node.MatchId is { } existingMatchId)
        {
            var existing = await _db.Matches.FirstOrDefaultAsync(m => m.Id == existingMatchId, ct);
            existing?.AssignTeams(node.HomeTeamId, node.AwayTeamId);
            return;
        }

        if (node.HomeTeamId is null || node.AwayTeamId is null)
            return;

        var match = new Domain.Entities.Match(
            sourceMatch.OrganizationId,
            sourceMatch.TournamentId,
            sourceMatch.DivisionId,
            sourceMatch.Wave,
            sourceMatch.ScheduledStartUtc,
            sourceMatch.DurationMinutes,
            bracketNodeId: node.Id,
            homeTeamId: node.HomeTeamId,
            awayTeamId: node.AwayTeamId,
            stageId: sourceMatch.StageId);

        _db.Matches.Add(match);
        node.LinkMatch(match.Id);
    }
}

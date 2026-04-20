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
            }
        }
        if (node.LosesToNodeId is { } losesTo && loserId is { })
        {
            var next = await _db.BracketNodes.FirstOrDefaultAsync(n => n.Id == losesTo, ct);
            if (next is not null)
            {
                next.AssignTeam(node.LosesToSlot ?? 2, loserId);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

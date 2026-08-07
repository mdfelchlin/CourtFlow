using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Application.Features.Teams;
using CourtFlow.Application.Matches;
using CourtFlow.Application.Standings;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Matches;

public sealed class MatchProgressionService : IMatchProgressionService
{
    private readonly AppDbContext _db;
    private readonly IBracketProgressionService _bracketProgression;
    private readonly IStandingsCalculator _standings;

    public MatchProgressionService(
        AppDbContext db,
        IBracketProgressionService bracketProgression,
        IStandingsCalculator standings)
    {
        _db = db;
        _bracketProgression = bracketProgression;
        _standings = standings;
    }

    public async Task OnMatchFinalizedAsync(Guid matchId, CancellationToken ct = default)
    {
        await _bracketProgression.AdvanceAsync(matchId, ct);

        var sourceMatch = await _db.Matches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (sourceMatch is null || sourceMatch.State != MatchState.Final)
            return;

        var winnerTt = await TournamentTeamIdAsync(sourceMatch.DivisionId, sourceMatch.WinnerTeamId, ct);
        var loserTeamId = sourceMatch.WinnerTeamId == sourceMatch.HomeTeamId
            ? sourceMatch.AwayTeamId
            : sourceMatch.HomeTeamId;
        var loserTt = await TournamentTeamIdAsync(sourceMatch.DivisionId, loserTeamId, ct);

        var dependents = await _db.MatchSlotSources
            .Where(s => s.SourceMatchId == matchId
                        && (s.SourceKind == MatchSlotSourceKind.MatchWinner
                            || s.SourceKind == MatchSlotSourceKind.MatchLoser))
            .ToListAsync(ct);

        foreach (var slot in dependents)
        {
            var ttId = slot.SourceKind == MatchSlotSourceKind.MatchWinner ? winnerTt : loserTt;
            slot.Resolve(ttId);
            await ApplySlotToMatchAsync(slot, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ResolveStandingsSourcesAsync(Guid divisionId, CancellationToken ct = default)
    {
        var standingResult = await _standings.ForDivisionAsync(divisionId, ct);
        var ttByTeamId = await _db.TournamentTeams
            .Where(tt => tt.DivisionId == divisionId)
            .ToDictionaryAsync(tt => tt.TeamId, tt => tt.Id, ct);

        var finishByPool = new Dictionary<(Guid poolId, int finish), Guid>();
        foreach (var poolStanding in standingResult.Pools)
        {
            foreach (var row in poolStanding.Rows)
            {
                if (ttByTeamId.TryGetValue(row.TeamId, out var ttId))
                    finishByPool[(poolStanding.PoolId, row.Rank)] = ttId;
            }
        }

        var rankByTt = await _db.DivisionRankings.AsNoTracking()
            .Where(r => r.DivisionId == divisionId)
            .ToDictionaryAsync(r => r.TournamentTeamId, r => r.Rank, ct);

        var slots = await _db.MatchSlotSources
            .Where(s => _db.Matches.Any(m => m.Id == s.MatchId && m.DivisionId == divisionId))
            .ToListAsync(ct);

        foreach (var slot in slots)
        {
            Guid? ttId = slot.SourceKind switch
            {
                MatchSlotSourceKind.PoolFinish when slot.SourcePoolId is { } poolId && slot.SourcePosition is { } finish
                    => finishByPool.GetValueOrDefault((poolId, finish)),
                MatchSlotSourceKind.Ranking when slot.SourcePosition is { } rank
                    => rankByTt.FirstOrDefault(kv => kv.Value == rank).Key is var id && id != Guid.Empty ? id : null,
                _ => slot.ResolvedTournamentTeamId
            };

            if (slot.SourceKind is MatchSlotSourceKind.PoolFinish or MatchSlotSourceKind.Ranking)
            {
                slot.Resolve(ttId);
                await ApplySlotToMatchAsync(slot, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplySlotToMatchAsync(MatchSlotSource slot, CancellationToken ct)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == slot.MatchId, ct);
        if (match is null) return;

        var teamId = await TeamIdFromTournamentTeamAsync(match.DivisionId, slot.ResolvedTournamentTeamId, ct);
        switch (slot.Slot)
        {
            case MatchSlotKind.Home:
                match.AssignTeams(teamId, match.AwayTeamId);
                break;
            case MatchSlotKind.Away:
                match.AssignTeams(match.HomeTeamId, teamId);
                break;
            case MatchSlotKind.Ref:
                match.AssignReferee(teamId);
                break;
        }
    }

    private async Task<Guid?> TournamentTeamIdAsync(Guid divisionId, Guid? teamId, CancellationToken ct)
    {
        if (teamId is null) return null;
        return await _db.TournamentTeams.AsNoTracking()
            .Where(tt => tt.DivisionId == divisionId && tt.TeamId == teamId)
            .Select(tt => (Guid?)tt.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Guid?> TeamIdFromTournamentTeamAsync(Guid divisionId, Guid? tournamentTeamId, CancellationToken ct)
    {
        if (tournamentTeamId is null) return null;
        return await _db.TournamentTeams.AsNoTracking()
            .Where(tt => tt.DivisionId == divisionId && tt.Id == tournamentTeamId)
            .Select(tt => (Guid?)tt.TeamId)
            .FirstOrDefaultAsync(ct);
    }
}

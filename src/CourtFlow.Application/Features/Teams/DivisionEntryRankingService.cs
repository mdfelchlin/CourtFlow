using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Persistence;
using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Teams;

/// <summary>
/// Keeps <c>tournament_teams</c> and <c>division_rankings</c> in sync with legacy <see cref="Team"/> rows.
/// Rankings live on <see cref="DivisionRanking"/> keyed by <see cref="TournamentTeam"/>.
/// <see cref="Team.SeedHint"/> may remain populated on legacy rows but is no longer authoritative for ranking.
/// </summary>
internal static class DivisionEntryRankingService
{
    public static async Task<TournamentTeam> EnsureTournamentTeamAsync(
        IAppDbContext db,
        Division division,
        Team team,
        CancellationToken ct)
    {
        var tournamentTeam = db.TournamentTeams.Local.FirstOrDefault(
            tt => tt.TournamentId == division.TournamentId && tt.TeamId == team.Id);
        tournamentTeam ??= await db.TournamentTeams.FirstOrDefaultAsync(
            tt => tt.TournamentId == division.TournamentId && tt.TeamId == team.Id,
            ct);

        if (tournamentTeam is null)
        {
            tournamentTeam = new TournamentTeam(division.OrganizationId, division.TournamentId, division.Id, team.Id);
            db.TournamentTeams.Add(tournamentTeam);
        }
        else if (tournamentTeam.DivisionId != division.Id)
        {
            tournamentTeam.MoveToDivision(division.Id);
        }

        return tournamentTeam;
    }

    public static async Task UpsertRankingAsync(
        IAppDbContext db,
        Division division,
        TournamentTeam tournamentTeam,
        int rank,
        string source,
        bool isManualOverride,
        CancellationToken ct)
    {
        var ranking = await db.DivisionRankings.FirstOrDefaultAsync(
            r => r.DivisionId == division.Id && r.TournamentTeamId == tournamentTeam.Id,
            ct);

        if (ranking is null)
        {
            ranking = new DivisionRanking(division.OrganizationId, division.Id, tournamentTeam.Id, rank, source);
            if (isManualOverride)
                ranking.SetRank(rank, source, isManualOverride: true);
            db.DivisionRankings.Add(ranking);
        }
        else
        {
            ranking.SetRank(rank, source, isManualOverride);
        }
    }

    public static async Task ReplaceDivisionRankingsAsync(
        IAppDbContext db,
        Division division,
        IReadOnlyList<(TournamentTeam TournamentTeam, int Rank)> ordered,
        string source,
        bool isManualOverride,
        CancellationToken ct)
    {
        var keepIds = ordered.Select(x => x.TournamentTeam.Id).ToList();
        await EntityGraphDeletes.ClearDivisionRankingsExceptAsync(db, division.Id, keepIds, ct);

        foreach (var (tournamentTeam, rank) in ordered)
            await UpsertRankingAsync(db, division, tournamentTeam, rank, source, isManualOverride, ct);
    }

    public static async Task<IReadOnlyDictionary<Guid, int>> GetRankByTeamIdAsync(
        IAppDbContext db,
        Guid divisionId,
        CancellationToken ct)
    {
        var rows = await (
            from ranking in db.DivisionRankings
            join tournamentTeam in db.TournamentTeams on ranking.TournamentTeamId equals tournamentTeam.Id
            where ranking.DivisionId == divisionId
            select new { tournamentTeam.TeamId, ranking.Rank }
        ).ToListAsync(ct);

        return rows.ToDictionary(x => x.TeamId, x => x.Rank);
    }

    public static async Task RemoveRankingsForTeamAsync(
        IAppDbContext db,
        Division division,
        Team team,
        CancellationToken ct)
    {
        var tournamentTeam = await db.TournamentTeams.FirstOrDefaultAsync(
            tt => tt.TournamentId == division.TournamentId && tt.TeamId == team.Id,
            ct);
        if (tournamentTeam is null)
            return;

        await EntityGraphDeletes.BulkDeleteQueryAsync(
            db,
            db.DivisionRankings.Where(r =>
                r.DivisionId == division.Id && r.TournamentTeamId == tournamentTeam.Id),
            ct);
    }

    public static async Task RemoveTournamentEntryAsync(
        IAppDbContext db,
        Division division,
        Team team,
        CancellationToken ct)
    {
        await RemoveRankingsForTeamAsync(db, division, team, ct);

        var tournamentTeam = await db.TournamentTeams.FirstOrDefaultAsync(
            tt => tt.TournamentId == division.TournamentId && tt.TeamId == team.Id,
            ct);
        if (tournamentTeam is not null)
            db.TournamentTeams.Remove(tournamentTeam);
    }


    public static async Task<int> NextRankAsync(IAppDbContext db, Guid divisionId, CancellationToken ct) =>
        (await db.DivisionRankings.Where(r => r.DivisionId == divisionId).Select(r => (int?)r.Rank).MaxAsync(ct)) is { } mx
            ? mx + 1
            : 1;

    /// <summary>Writes compat <see cref="Team.SeedHint"/> to mirror canonical rank.</summary>
    public static void MirrorSeedHint(Team team, int rank)
    {
#pragma warning disable CS0618
        team.SetSeedHint(rank);
#pragma warning restore CS0618
    }
}


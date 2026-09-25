using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Persistence;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Teams;

public sealed record TeamDto(
    Guid Id,
    Guid? TournamentTeamId,
    Guid DivisionId,
    Guid? PoolId,
    string Name,
    string? Club,
    Wave Wave,
    int? SeedHint,
    int? SeedInPool);

internal static class TeamMapping
{
    public static TeamDto ToDto(
        this Team team,
        int? displayRank = null,
        Guid? tournamentTeamId = null,
        int? seedInPool = null,
        Guid? poolId = null) =>
        new(team.Id, tournamentTeamId, team.DivisionId, poolId ?? team.PoolId, team.Name, team.Club, team.Wave, displayRank ?? team.SeedHint, seedInPool);
}

public sealed record ListTeamsQuery(Guid? TournamentId, Guid? DivisionId, Guid? PoolId) : IRequest<IReadOnlyList<TeamDto>>;

public sealed class ListTeamsHandler : IRequestHandler<ListTeamsQuery, IReadOnlyList<TeamDto>>
{
    private readonly IAppDbContext _db;
    public ListTeamsHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<TeamDto>> Handle(ListTeamsQuery request, CancellationToken ct)
    {
        var q = _db.Teams.AsQueryable();
        if (request.TournamentId is { } tournamentId)
        {
            var divisionIdsInTournament = _db.Divisions.Where(d => d.TournamentId == tournamentId).Select(d => d.Id);
            q = q.Where(t => divisionIdsInTournament.Contains(t.DivisionId));
        }
        if (request.DivisionId is { } d) q = q.Where(t => t.DivisionId == d);
        if (request.PoolId is { } p) q = q.Where(t => t.PoolId == p);
        var items = await q.ToListAsync(ct);

        IReadOnlyDictionary<Guid, int>? ranksByTeamId = null;
        if (request.DivisionId is { } divisionId)
            ranksByTeamId = await DivisionEntryRankingService.GetRankByTeamIdAsync(_db, divisionId, ct);

        Guid? tidForLookup = request.TournamentId;
        if (tidForLookup is null && request.DivisionId is { } divisionIdForTournament)
        {
            tidForLookup = await _db.Divisions.AsNoTracking()
                .Where(d => d.Id == divisionIdForTournament)
                .Select(d => d.TournamentId)
                .FirstOrDefaultAsync(ct);
        }

        IReadOnlyDictionary<Guid, Guid> tournamentTeamByTeamId = new Dictionary<Guid, Guid>();
        if (tidForLookup is { } tid && tid != Guid.Empty)
        {
            tournamentTeamByTeamId = await _db.TournamentTeams.AsNoTracking()
                .Where(tt => tt.TournamentId == tid)
                .ToDictionaryAsync(tt => tt.TeamId, tt => tt.Id, ct);
        }

        IReadOnlyDictionary<Guid, int> seedInPoolByTeamId = new Dictionary<Guid, int>();
        IReadOnlyDictionary<Guid, Guid> poolIdByTeamId = new Dictionary<Guid, Guid>();
        if (request.DivisionId is { } divIdForPools && tournamentTeamByTeamId.Count > 0)
        {
            var poolIds = request.PoolId is { } filterPoolId
                ? new List<Guid> { filterPoolId }
                : await _db.Pools.AsNoTracking()
                    .Where(p => p.DivisionId == divIdForPools)
                    .Select(p => p.Id)
                    .ToListAsync(ct);

            if (poolIds.Count > 0)
            {
                var teamIdByTournamentTeamId = tournamentTeamByTeamId.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
                var entries = await _db.PoolEntries.AsNoTracking()
                    .Where(e => poolIds.Contains(e.PoolId))
                    .Select(e => new { e.PoolId, e.TournamentTeamId, e.SeedInPool })
                    .ToListAsync(ct);

                seedInPoolByTeamId = entries
                    .Where(e => teamIdByTournamentTeamId.ContainsKey(e.TournamentTeamId))
                    .ToDictionary(e => teamIdByTournamentTeamId[e.TournamentTeamId], e => e.SeedInPool);
                poolIdByTeamId = entries
                    .Where(e => teamIdByTournamentTeamId.ContainsKey(e.TournamentTeamId))
                    .ToDictionary(e => teamIdByTournamentTeamId[e.TournamentTeamId], e => e.PoolId);
            }
        }

        int RankKey(Team t) => ranksByTeamId?.GetValueOrDefault(t.Id) ?? t.SeedHint ?? int.MaxValue;
        int PoolSeedKey(Team t) => seedInPoolByTeamId.GetValueOrDefault(t.Id, int.MaxValue);

        if (request.PoolId is not null)
            items = items.OrderBy(PoolSeedKey).ThenBy(RankKey).ThenBy(t => t.Name).ToList();
        else if (request.DivisionId is not null)
            items = items.OrderBy(RankKey).ThenBy(t => t.Id).ToList();
        else
            items = items.OrderBy(t => t.Name).ToList();

        return items.Select(t =>
        {
            var rank = ranksByTeamId?.GetValueOrDefault(t.Id);
            Guid? ttId = tournamentTeamByTeamId.TryGetValue(t.Id, out var found) ? found : null;
            int? seedInPool = seedInPoolByTeamId.TryGetValue(t.Id, out var sip) ? sip : null;
            Guid? poolId = poolIdByTeamId.TryGetValue(t.Id, out var pid) ? pid : null;
            return t.ToDto(rank, ttId, seedInPool, poolId);
        }).ToList();
    }
}

public sealed record CreateTeamCommand(Guid DivisionId, string Name, string? Club) : IRequest<TeamDto>;

public sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator()
    {
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateTeamHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateTeamHandler(IAppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }
    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var division = await _db.Divisions.FirstAsync(d => d.Id == request.DivisionId, ct);
        var team = new Team(orgId, request.DivisionId, request.Name, Wave.AM, poolId: null, club: request.Club, seedHint: null);
        _db.Teams.Add(team);
        var tt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);
        var rank = await DivisionEntryRankingService.NextRankAsync(_db, division.Id, ct);
        await DivisionEntryRankingService.UpsertRankingAsync(
            _db, division, tt, rank, source: "manual", isManualOverride: false, ct);
        await _db.SaveChangesAsync(ct);
        return team.ToDto(rank);
    }
}

public sealed record UpdateTeamCommand(Guid Id, string Name, string? Club) : IRequest<TeamDto?>;

public sealed class UpdateTeamValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateTeamHandler : IRequestHandler<UpdateTeamCommand, TeamDto?>
{
    private readonly IAppDbContext _db;
    public UpdateTeamHandler(IAppDbContext db) => _db = db;
    public async Task<TeamDto?> Handle(UpdateTeamCommand request, CancellationToken ct)
    {
        var t = await _db.Teams.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;
        t.Rename(request.Name);
        t.SetClub(request.Club);
        await _db.SaveChangesAsync(ct);
        return t.ToDto();
    }
}

public sealed record MoveTeamCommand(Guid TeamId, Guid DivisionId) : IRequest<TeamDto?>;

public sealed class MoveTeamValidator : AbstractValidator<MoveTeamCommand>
{
    public MoveTeamValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.DivisionId).NotEmpty();
    }
}

public sealed class MoveTeamHandler : IRequestHandler<MoveTeamCommand, TeamDto?>
{
    private readonly IAppDbContext _db;
    public MoveTeamHandler(IAppDbContext db) => _db = db;
    public async Task<TeamDto?> Handle(MoveTeamCommand request, CancellationToken ct)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(x => x.Id == request.TeamId, ct);
        if (team is null) return null;
        var toDiv = await _db.Divisions.FirstOrDefaultAsync(x => x.Id == request.DivisionId, ct);
        if (toDiv is null) return null;
        if (toDiv.OrganizationId != team.OrganizationId) return null;
        var fromDiv = await _db.Divisions.FirstOrDefaultAsync(x => x.Id == team.DivisionId, ct);
        if (fromDiv is null) return null;
        if (fromDiv.TournamentId != toDiv.TournamentId) return null;

        await DivisionEntryRankingService.RemoveRankingsForTeamAsync(_db, fromDiv, team, ct);
        team.MoveToDivision(request.DivisionId);
        var tt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, toDiv, team, ct);
        var rank = await DivisionEntryRankingService.NextRankAsync(_db, toDiv.Id, ct);
        await DivisionEntryRankingService.UpsertRankingAsync(
            _db, toDiv, tt, rank, source: "manual", isManualOverride: false, ct);
        await _db.SaveChangesAsync(ct);
        return team.ToDto();
    }
}

public sealed record AssignTeamPoolCommand(Guid TeamId, Guid? PoolId) : IRequest<AssignTeamPoolResult?>;

public sealed record AssignTeamPoolResult(TeamDto Team, bool ClearedPoolMatches);

public sealed class AssignTeamPoolValidator : AbstractValidator<AssignTeamPoolCommand>
{
    public AssignTeamPoolValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
    }
}

public sealed class AssignTeamPoolHandler : IRequestHandler<AssignTeamPoolCommand, AssignTeamPoolResult?>
{
    private readonly IAppDbContext _db;
    public AssignTeamPoolHandler(IAppDbContext db) => _db = db;

    public async Task<AssignTeamPoolResult?> Handle(AssignTeamPoolCommand request, CancellationToken ct)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == request.TeamId, ct);
        if (team is null) return null;

        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == team.DivisionId, ct);
        if (division is null) return null;

        Pool? pool = null;
        if (request.PoolId is { } pid)
        {
            pool = await _db.Pools.FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (pool is null || pool.DivisionId != team.DivisionId) return null;
        }

        // No-op when unchanged — MudSelect can re-fire the current value (or null) on bind.
        if (team.PoolId == request.PoolId)
        {
            var existingTt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);
            var existingSeed = await _db.PoolEntries.AsNoTracking()
                .Where(e => e.TournamentTeamId == existingTt.Id)
                .Select(e => (int?)e.SeedInPool)
                .FirstOrDefaultAsync(ct);
            return new AssignTeamPoolResult(
                team.ToDto(tournamentTeamId: existingTt.Id, seedInPool: existingSeed, poolId: team.PoolId),
                ClearedPoolMatches: false);
        }

        var affectedPoolIds = new HashSet<Guid>();
        if (team.PoolId is { } fromPoolId)
            affectedPoolIds.Add(fromPoolId);
        if (request.PoolId is { } toPoolId)
            affectedPoolIds.Add(toPoolId);

        var cleared = await PoolTeamPlacement.ClearPoolMatchesIfAnyAsync(_db, division, affectedPoolIds, ct);

        team.AssignPool(request.PoolId);

        var tt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);

        var oldEntries = await _db.PoolEntries
            .Where(e => e.TournamentTeamId == tt.Id)
            .ToListAsync(ct);
        foreach (var entry in oldEntries)
            _db.PoolEntries.Remove(entry);

        int? seedInPool = null;
        if (pool is not null)
        {
            seedInPool = await _db.PoolEntries.CountAsync(e => e.PoolId == pool.Id, ct) + 1;
            _db.PoolEntries.Add(new PoolEntry(division.OrganizationId, pool.Id, tt.Id, seedInPool.Value));
            if (seedInPool.Value > pool.TargetSize && seedInPool.Value <= 8)
                pool.SetTargetSize(seedInPool.Value);
        }

        await _db.SaveChangesAsync(ct);
        return new AssignTeamPoolResult(
            team.ToDto(seedInPool: seedInPool, poolId: request.PoolId),
            ClearedPoolMatches: cleared);
    }
}

public sealed record SwapTeamPoolsCommand(Guid TeamId, Guid OtherTeamId) : IRequest<SwapTeamPoolsResult?>;

public sealed record SwapTeamPoolsResult(TeamDto Team, TeamDto OtherTeam);

public sealed class SwapTeamPoolsValidator : AbstractValidator<SwapTeamPoolsCommand>
{
    public SwapTeamPoolsValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.OtherTeamId).NotEmpty();
        RuleFor(x => x.OtherTeamId).NotEqual(x => x.TeamId);
    }
}

public sealed class SwapTeamPoolsHandler : IRequestHandler<SwapTeamPoolsCommand, SwapTeamPoolsResult?>
{
    private readonly IAppDbContext _db;
    public SwapTeamPoolsHandler(IAppDbContext db) => _db = db;

    public async Task<SwapTeamPoolsResult?> Handle(SwapTeamPoolsCommand request, CancellationToken ct)
    {
        var teamA = await _db.Teams.FirstOrDefaultAsync(t => t.Id == request.TeamId, ct);
        var teamB = await _db.Teams.FirstOrDefaultAsync(t => t.Id == request.OtherTeamId, ct);
        if (teamA is null || teamB is null) return null;
        if (teamA.DivisionId != teamB.DivisionId) return null;

        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == teamA.DivisionId, ct);
        if (division is null) return null;

        var ttA = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, teamA, ct);
        var ttB = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, teamB, ct);

        var entryA = await _db.PoolEntries.FirstOrDefaultAsync(e => e.TournamentTeamId == ttA.Id, ct);
        var entryB = await _db.PoolEntries.FirstOrDefaultAsync(e => e.TournamentTeamId == ttB.Id, ct);
        if (entryA is null || entryB is null) return null;

        var poolIdA = entryA.PoolId;
        var seedA = entryA.SeedInPool;
        var poolIdB = entryB.PoolId;
        var seedB = entryB.SeedInPool;

        entryA.Reassign(poolIdB, seedB);
        entryB.Reassign(poolIdA, seedA);
        teamA.AssignPool(poolIdB);
        teamB.AssignPool(poolIdA);

        await PoolTeamPlacement.RemapPoolMatchTeamsAsync(
            _db,
            division.Id,
            teamA.Id,
            teamB.Id,
            ttA.Id,
            ttB.Id,
            ct);

        await _db.SaveChangesAsync(ct);
        return new SwapTeamPoolsResult(
            teamA.ToDto(tournamentTeamId: ttA.Id, seedInPool: seedB, poolId: poolIdB),
            teamB.ToDto(tournamentTeamId: ttB.Id, seedInPool: seedA, poolId: poolIdA));
    }
}

internal static class PoolTeamPlacement
{
    public static async Task<bool> ClearPoolMatchesIfAnyAsync(
        IAppDbContext db,
        Division division,
        IReadOnlyCollection<Guid> poolIds,
        CancellationToken ct)
    {
        if (poolIds.Count == 0) return false;

        var poolStage = await db.Stages
            .Where(s => s.DivisionId == division.Id && s.StageType == StageType.Pool)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);
        if (poolStage is null) return false;

        var hasMatches = await db.Matches.AnyAsync(
            m => m.DivisionId == division.Id
                 && m.StageId == poolStage.Id
                 && m.PoolId != null
                 && poolIds.Contains(m.PoolId.Value),
            ct);
        if (!hasMatches) return false;

        var scheduleVersion = await ScheduleVersionProvisioning.EnsureCurrentAsync(db, division.TournamentId, ct);
        await EntityGraphDeletes.ClearPoolPlayMatchesForPoolsAsync(
            db, division.Id, poolStage.Id, scheduleVersion.Id, poolIds, ct);
        return true;
    }

    public static async Task RemapPoolMatchTeamsAsync(
        IAppDbContext db,
        Guid divisionId,
        Guid teamAId,
        Guid teamBId,
        Guid tournamentTeamAId,
        Guid tournamentTeamBId,
        CancellationToken ct)
    {
        var matches = await db.Matches
            .Where(m => m.DivisionId == divisionId
                        && m.PoolId != null
                        && (m.HomeTeamId == teamAId
                            || m.AwayTeamId == teamAId
                            || m.RefTeamId == teamAId
                            || m.HomeTeamId == teamBId
                            || m.AwayTeamId == teamBId
                            || m.RefTeamId == teamBId))
            .ToListAsync(ct);

        foreach (var match in matches)
        {
            var home = SwapId(match.HomeTeamId, teamAId, teamBId);
            var away = SwapId(match.AwayTeamId, teamAId, teamBId);
            var refTeam = SwapId(match.RefTeamId, teamAId, teamBId);
            match.AssignTeams(home, away);
            match.AssignReferee(refTeam);
        }

        var matchIds = matches.Select(m => m.Id).ToList();
        if (matchIds.Count == 0) return;

        var slots = await db.MatchSlotSources
            .Where(s => matchIds.Contains(s.MatchId)
                        && s.SourceKind == MatchSlotSourceKind.FixedTeam
                        && (s.FixedTournamentTeamId == tournamentTeamAId
                            || s.FixedTournamentTeamId == tournamentTeamBId))
            .ToListAsync(ct);

        foreach (var slot in slots)
        {
            if (slot.FixedTournamentTeamId == tournamentTeamAId)
                slot.ConfigureFixedTeam(tournamentTeamBId);
            else if (slot.FixedTournamentTeamId == tournamentTeamBId)
                slot.ConfigureFixedTeam(tournamentTeamAId);
        }

        var officials = await db.MatchOfficials
            .Where(o => matchIds.Contains(o.MatchId)
                        && (o.WorkTeamTournamentTeamId == tournamentTeamAId
                            || o.WorkTeamTournamentTeamId == tournamentTeamBId))
            .ToListAsync(ct);

        foreach (var official in officials)
        {
            var work = official.WorkTeamTournamentTeamId == tournamentTeamAId
                ? tournamentTeamBId
                : tournamentTeamAId;
            official.ReassignWorkTeam(work, official.AssignmentSource);
        }
    }

    private static Guid? SwapId(Guid? current, Guid a, Guid b)
    {
        if (current == a) return b;
        if (current == b) return a;
        return current;
    }
}

public sealed record ReorderDivisionTeamsCommand(Guid DivisionId, IReadOnlyList<Guid> OrderedTeamIds) : IRequest<IReadOnlyList<TeamDto>?>;

public sealed class ReorderDivisionTeamsValidator : AbstractValidator<ReorderDivisionTeamsCommand>
{
    public ReorderDivisionTeamsValidator()
    {
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.OrderedTeamIds).NotEmpty();
    }
}

public sealed class ReorderDivisionTeamsHandler : IRequestHandler<ReorderDivisionTeamsCommand, IReadOnlyList<TeamDto>?>
{
    private readonly IAppDbContext _db;
    public ReorderDivisionTeamsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TeamDto>?> Handle(ReorderDivisionTeamsCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var teams = await _db.Teams.Where(t => t.DivisionId == request.DivisionId).ToListAsync(ct);
        var idSet = teams.Select(t => t.Id).ToHashSet();
        if (request.OrderedTeamIds.Count != teams.Count || request.OrderedTeamIds.Any(id => !idSet.Contains(id)))
            return null;

        var orderedEntries = new List<(TournamentTeam TournamentTeam, int Rank)>(request.OrderedTeamIds.Count);
        for (var i = 0; i < request.OrderedTeamIds.Count; i++)
        {
            var rank = i + 1;
            var team = teams.Single(t => t.Id == request.OrderedTeamIds[i]);
            var tournamentTeam = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);
            DivisionEntryRankingService.MirrorSeedHint(team, rank);
            orderedEntries.Add((tournamentTeam, rank));
        }

        await DivisionEntryRankingService.ReplaceDivisionRankingsAsync(
            _db, division, orderedEntries, source: "manual", isManualOverride: true, ct);

        await _db.SaveChangesAsync(ct);

        return request.OrderedTeamIds
            .Select(id => teams.Single(t => t.Id == id))
            .Select((t, i) => t.ToDto(i + 1))
            .ToList();
    }
}

public sealed record RandomizeDivisionSeedsCommand(Guid DivisionId) : IRequest<IReadOnlyList<TeamDto>?>;

public sealed class RandomizeDivisionSeedsValidator : AbstractValidator<RandomizeDivisionSeedsCommand>
{
    public RandomizeDivisionSeedsValidator() => RuleFor(x => x.DivisionId).NotEmpty();
}

public sealed class RandomizeDivisionSeedsHandler : IRequestHandler<RandomizeDivisionSeedsCommand, IReadOnlyList<TeamDto>?>
{
    private readonly IAppDbContext _db;
    public RandomizeDivisionSeedsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TeamDto>?> Handle(RandomizeDivisionSeedsCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var teams = await _db.Teams.Where(t => t.DivisionId == request.DivisionId).ToListAsync(ct);
        if (teams.Count == 0) return Array.Empty<TeamDto>();

        var rnd = new Random();
        var shuffled = teams.OrderBy(_ => rnd.Next()).ToList();
        var orderedEntries = new List<(TournamentTeam TournamentTeam, int Rank)>(shuffled.Count);
        for (var i = 0; i < shuffled.Count; i++)
        {
            var rank = i + 1;
            var team = shuffled[i];
            var tournamentTeam = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);
            DivisionEntryRankingService.MirrorSeedHint(team, rank);
            orderedEntries.Add((tournamentTeam, rank));
        }

        await DivisionEntryRankingService.ReplaceDivisionRankingsAsync(
            _db, division, orderedEntries, source: "random", isManualOverride: false, ct);

        await _db.SaveChangesAsync(ct);
        return shuffled.Select((t, i) => t.ToDto(i + 1)).ToList();
    }
}

public sealed record DeleteTeamCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteTeamHandler : IRequestHandler<DeleteTeamCommand, bool>
{
    private readonly IAppDbContext _db;
    public DeleteTeamHandler(IAppDbContext db) => _db = db;
    public async Task<bool> Handle(DeleteTeamCommand request, CancellationToken ct)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (team is null) return false;
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == team.DivisionId, ct);
        if (division is not null)
            await DivisionEntryRankingService.RemoveTournamentEntryAsync(_db, division, team, ct);
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

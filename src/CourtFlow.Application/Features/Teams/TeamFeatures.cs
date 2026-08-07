using CourtFlow.Application.Abstractions;
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
    int? SeedHint);

internal static class TeamMapping
{
    public static TeamDto ToDto(this Team team, int? displayRank = null, Guid? tournamentTeamId = null) =>
        new(team.Id, tournamentTeamId, team.DivisionId, team.PoolId, team.Name, team.Club, team.Wave, displayRank ?? team.SeedHint);
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

        int RankKey(Team t) => ranksByTeamId?.GetValueOrDefault(t.Id) ?? t.SeedHint ?? int.MaxValue;

        if (request.PoolId is not null)
            items = items.OrderBy(RankKey).ThenBy(t => t.Name).ToList();
        else if (request.DivisionId is not null)
            items = items.OrderBy(RankKey).ThenBy(t => t.Id).ToList();
        else
            items = items.OrderBy(t => t.Name).ToList();

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

        return items.Select(t =>
        {
            var rank = ranksByTeamId?.GetValueOrDefault(t.Id);
            Guid? ttId = tournamentTeamByTeamId.TryGetValue(t.Id, out var found) ? found : null;
            return t.ToDto(rank, ttId);
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

public sealed record AssignTeamPoolCommand(Guid TeamId, Guid? PoolId) : IRequest<TeamDto?>;

public sealed class AssignTeamPoolValidator : AbstractValidator<AssignTeamPoolCommand>
{
    public AssignTeamPoolValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
    }
}

public sealed class AssignTeamPoolHandler : IRequestHandler<AssignTeamPoolCommand, TeamDto?>
{
    private readonly IAppDbContext _db;
    public AssignTeamPoolHandler(IAppDbContext db) => _db = db;

    public async Task<TeamDto?> Handle(AssignTeamPoolCommand request, CancellationToken ct)
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

        team.AssignPool(request.PoolId);

        var tt = await DivisionEntryRankingService.EnsureTournamentTeamAsync(_db, division, team, ct);

        var oldEntries = await _db.PoolEntries
            .Where(e => e.TournamentTeamId == tt.Id)
            .ToListAsync(ct);
        foreach (var entry in oldEntries)
            _db.PoolEntries.Remove(entry);

        if (pool is not null)
        {
            var seedInPool = await _db.PoolEntries.CountAsync(e => e.PoolId == pool.Id, ct) + 1;
            _db.PoolEntries.Add(new PoolEntry(division.OrganizationId, pool.Id, tt.Id, seedInPool));
        }

        await _db.SaveChangesAsync(ct);
        return team.ToDto();
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

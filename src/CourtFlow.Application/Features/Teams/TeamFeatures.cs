using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Teams;

public sealed record TeamDto(Guid Id, Guid DivisionId, Guid? PoolId, string Name, string? Club, Wave Wave, int? SeedHint);

internal static class TeamMapping
{
    public static TeamDto ToDto(this Team t) => new(t.Id, t.DivisionId, t.PoolId, t.Name, t.Club, t.Wave, t.SeedHint);
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
        var items = await q.OrderBy(t => t.Name).ToListAsync(ct);
        return items.Select(TeamMapping.ToDto).ToList();
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
        var t = new Team(orgId, request.DivisionId, request.Name, Wave.AM, poolId: null, club: request.Club, seedHint: null);
        _db.Teams.Add(t);
        await _db.SaveChangesAsync(ct);
        return t.ToDto();
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
        team.MoveToDivision(request.DivisionId);
        await _db.SaveChangesAsync(ct);
        return team.ToDto();
    }
}

public sealed record DeleteTeamCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteTeamHandler : IRequestHandler<DeleteTeamCommand, bool>
{
    private readonly IAppDbContext _db;
    public DeleteTeamHandler(IAppDbContext db) => _db = db;
    public async Task<bool> Handle(DeleteTeamCommand request, CancellationToken ct)
    {
        var t = await _db.Teams.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return false;
        _db.Teams.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

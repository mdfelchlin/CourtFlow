using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Divisions;

public sealed record DivisionDto(Guid Id, Guid TournamentId, string Name, DivisionFormat Format, int MaxTeams);

internal static class DivisionMapping
{
    public static DivisionDto ToDto(this Division d) => new(d.Id, d.TournamentId, d.Name, d.Format, d.MaxTeams);
}

public sealed record ListDivisionsQuery(Guid? TournamentId) : IRequest<IReadOnlyList<DivisionDto>>;

public sealed class ListDivisionsHandler : IRequestHandler<ListDivisionsQuery, IReadOnlyList<DivisionDto>>
{
    private readonly IAppDbContext _db;
    public ListDivisionsHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<DivisionDto>> Handle(ListDivisionsQuery request, CancellationToken ct)
    {
        var q = _db.Divisions.AsQueryable();
        if (request.TournamentId is { } t) q = q.Where(d => d.TournamentId == t);
        var items = await q.OrderBy(d => d.Name).ToListAsync(ct);
        return items.Select(DivisionMapping.ToDto).ToList();
    }
}

public sealed record CreateDivisionCommand(Guid TournamentId, string Name, DivisionFormat Format, int MaxTeams) : IRequest<DivisionDto>;

public sealed class CreateDivisionValidator : AbstractValidator<CreateDivisionCommand>
{
    public CreateDivisionValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MaxTeams).GreaterThan(0);
    }
}

public sealed class CreateDivisionHandler : IRequestHandler<CreateDivisionCommand, DivisionDto>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateDivisionHandler(IAppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }
    public async Task<DivisionDto> Handle(CreateDivisionCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var d = new Division(orgId, request.TournamentId, request.Name, request.Format, request.MaxTeams);
        _db.Divisions.Add(d);
        await _db.SaveChangesAsync(ct);
        return d.ToDto();
    }
}

public sealed record UpdateDivisionCommand(Guid Id, string Name, DivisionFormat Format, int MaxTeams) : IRequest<DivisionDto?>;

public sealed class UpdateDivisionHandler : IRequestHandler<UpdateDivisionCommand, DivisionDto?>
{
    private readonly IAppDbContext _db;
    public UpdateDivisionHandler(IAppDbContext db) => _db = db;
    public async Task<DivisionDto?> Handle(UpdateDivisionCommand request, CancellationToken ct)
    {
        var d = await _db.Divisions.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (d is null) return null;
        d.Rename(request.Name);
        d.SetFormat(request.Format);
        d.SetMaxTeams(request.MaxTeams);
        await _db.SaveChangesAsync(ct);
        return d.ToDto();
    }
}

public sealed record DeleteDivisionCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteDivisionHandler : IRequestHandler<DeleteDivisionCommand, bool>
{
    private readonly IAppDbContext _db;
    public DeleteDivisionHandler(IAppDbContext db) => _db = db;
    public async Task<bool> Handle(DeleteDivisionCommand request, CancellationToken ct)
    {
        var d = await _db.Divisions.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (d is null) return false;
        _db.Divisions.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

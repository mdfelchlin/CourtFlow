using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Venues;

public sealed record VenueDto(Guid Id, Guid TournamentId, string Name, string? Address);

internal static class VenueMapping
{
    public static VenueDto ToDto(this Venue v) => new(v.Id, v.TournamentId, v.Name, v.Address);
}

public sealed record ListVenuesQuery(Guid? TournamentId) : IRequest<IReadOnlyList<VenueDto>>;

public sealed class ListVenuesHandler : IRequestHandler<ListVenuesQuery, IReadOnlyList<VenueDto>>
{
    private readonly IAppDbContext _db;
    public ListVenuesHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<VenueDto>> Handle(ListVenuesQuery request, CancellationToken ct)
    {
        var q = _db.Venues.AsQueryable();
        if (request.TournamentId is { } t) q = q.Where(v => v.TournamentId == t);
        var items = await q.OrderBy(v => v.Name).ToListAsync(ct);
        return items.Select(VenueMapping.ToDto).ToList();
    }
}

public sealed record GetVenueQuery(Guid Id) : IRequest<VenueDto?>;

public sealed class GetVenueHandler : IRequestHandler<GetVenueQuery, VenueDto?>
{
    private readonly IAppDbContext _db;
    public GetVenueHandler(IAppDbContext db) => _db = db;
    public async Task<VenueDto?> Handle(GetVenueQuery request, CancellationToken ct)
    {
        var v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return v?.ToDto();
    }
}

public sealed record CreateVenueCommand(Guid TournamentId, string Name, string? Address) : IRequest<VenueDto>;

public sealed class CreateVenueValidator : AbstractValidator<CreateVenueCommand>
{
    public CreateVenueValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateVenueHandler : IRequestHandler<CreateVenueCommand, VenueDto>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateVenueHandler(IAppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }
    public async Task<VenueDto> Handle(CreateVenueCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var v = new Venue(orgId, request.TournamentId, request.Name, request.Address);
        _db.Venues.Add(v);
        await _db.SaveChangesAsync(ct);
        return v.ToDto();
    }
}

public sealed record UpdateVenueCommand(Guid Id, string Name, string? Address) : IRequest<VenueDto?>;

public sealed class UpdateVenueHandler : IRequestHandler<UpdateVenueCommand, VenueDto?>
{
    private readonly IAppDbContext _db;
    public UpdateVenueHandler(IAppDbContext db) => _db = db;
    public async Task<VenueDto?> Handle(UpdateVenueCommand request, CancellationToken ct)
    {
        var v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (v is null) return null;
        v.Rename(request.Name);
        v.SetAddress(request.Address);
        await _db.SaveChangesAsync(ct);
        return v.ToDto();
    }
}

public sealed record DeleteVenueCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteVenueHandler : IRequestHandler<DeleteVenueCommand, bool>
{
    private readonly IAppDbContext _db;
    public DeleteVenueHandler(IAppDbContext db) => _db = db;
    public async Task<bool> Handle(DeleteVenueCommand request, CancellationToken ct)
    {
        var v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (v is null) return false;
        _db.Venues.Remove(v);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

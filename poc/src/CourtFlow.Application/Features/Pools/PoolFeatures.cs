using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Pools;

public sealed record PoolDto(Guid Id, Guid DivisionId, string Name);

internal static class PoolMapping
{
    public static PoolDto ToDto(this Pool p) => new(p.Id, p.DivisionId, p.Name);
}

public sealed record ListPoolsQuery(Guid? DivisionId) : IRequest<IReadOnlyList<PoolDto>>;

public sealed class ListPoolsHandler : IRequestHandler<ListPoolsQuery, IReadOnlyList<PoolDto>>
{
    private readonly IAppDbContext _db;
    public ListPoolsHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<PoolDto>> Handle(ListPoolsQuery request, CancellationToken ct)
    {
        var q = _db.Pools.AsQueryable();
        if (request.DivisionId is { } d) q = q.Where(p => p.DivisionId == d);
        var items = await q.OrderBy(p => p.Name).ToListAsync(ct);
        return items.Select(PoolMapping.ToDto).ToList();
    }
}

public sealed record CreatePoolCommand(Guid DivisionId, string Name) : IRequest<PoolDto>;

public sealed class CreatePoolValidator : AbstractValidator<CreatePoolCommand>
{
    public CreatePoolValidator()
    {
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreatePoolHandler : IRequestHandler<CreatePoolCommand, PoolDto>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreatePoolHandler(IAppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }
    public async Task<PoolDto> Handle(CreatePoolCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var p = new Pool(orgId, request.DivisionId, request.Name);
        _db.Pools.Add(p);
        await _db.SaveChangesAsync(ct);
        return p.ToDto();
    }
}

public sealed record UpdatePoolCommand(Guid Id, string Name) : IRequest<PoolDto?>;

public sealed class UpdatePoolHandler : IRequestHandler<UpdatePoolCommand, PoolDto?>
{
    private readonly IAppDbContext _db;
    public UpdatePoolHandler(IAppDbContext db) => _db = db;
    public async Task<PoolDto?> Handle(UpdatePoolCommand request, CancellationToken ct)
    {
        var p = await _db.Pools.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (p is null) return null;
        p.Rename(request.Name);
        await _db.SaveChangesAsync(ct);
        return p.ToDto();
    }
}

public sealed record DeletePoolCommand(Guid Id) : IRequest<bool>;

public sealed class DeletePoolHandler : IRequestHandler<DeletePoolCommand, bool>
{
    private readonly IAppDbContext _db;
    public DeletePoolHandler(IAppDbContext db) => _db = db;
    public async Task<bool> Handle(DeletePoolCommand request, CancellationToken ct)
    {
        var p = await _db.Pools.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (p is null) return false;
        var assigned = await _db.Teams.Where(t => t.PoolId == p.Id).ToListAsync(ct);
        foreach (var t in assigned)
            t.AssignPool(null);
        _db.Pools.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

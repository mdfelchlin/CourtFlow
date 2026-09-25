using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Courts;

public sealed record CourtDto(Guid Id, Guid VenueId, string Name, string[] Tags);

internal static class CourtMapping
{
    public static CourtDto ToDto(this Court c) => new(c.Id, c.VenueId, c.Name, c.Tags);
}

public sealed record ListCourtsQuery(Guid? VenueId) : IRequest<IReadOnlyList<CourtDto>>;

public sealed class ListCourtsHandler : IRequestHandler<ListCourtsQuery, IReadOnlyList<CourtDto>>
{
    private readonly IAppDbContext _db;
    public ListCourtsHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<CourtDto>> Handle(ListCourtsQuery request, CancellationToken ct)
    {
        var q = _db.Courts.AsQueryable();
        if (request.VenueId is { } v) q = q.Where(c => c.VenueId == v);
        var items = await q.OrderBy(c => c.Name).ToListAsync(ct);
        return items.Select(CourtMapping.ToDto).ToList();
    }
}

public sealed record CreateCourtCommand(Guid VenueId, string Name, string[] Tags) : IRequest<CourtDto>;

public sealed class CreateCourtValidator : AbstractValidator<CreateCourtCommand>
{
    public CreateCourtValidator()
    {
        RuleFor(x => x.VenueId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateCourtHandler : IRequestHandler<CreateCourtCommand, CourtDto>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateCourtHandler(IAppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }
    public async Task<CourtDto> Handle(CreateCourtCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var c = new Court(orgId, request.VenueId, request.Name, request.Tags);
        _db.Courts.Add(c);
        await _db.SaveChangesAsync(ct);
        return c.ToDto();
    }
}

public sealed record UpdateCourtCommand(Guid Id, string Name, string[] Tags) : IRequest<CourtDto?>;

public sealed class UpdateCourtHandler : IRequestHandler<UpdateCourtCommand, CourtDto?>
{
    private readonly IAppDbContext _db;
    public UpdateCourtHandler(IAppDbContext db) => _db = db;
    public async Task<CourtDto?> Handle(UpdateCourtCommand request, CancellationToken ct)
    {
        var c = await _db.Courts.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (c is null) return null;
        c.Rename(request.Name);
        c.SetTags(request.Tags);
        await _db.SaveChangesAsync(ct);
        return c.ToDto();
    }
}

public sealed record DeleteCourtCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteCourtHandler : IRequestHandler<DeleteCourtCommand, bool>
{
    private readonly IAppDbContext _db;
    public DeleteCourtHandler(IAppDbContext db) => _db = db;
    public async Task<bool> Handle(DeleteCourtCommand request, CancellationToken ct)
    {
        var c = await _db.Courts.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (c is null) return false;
        _db.Courts.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record DelayCourtCommand(Guid CourtId, int Slots) : IRequest<int>;

public sealed class DelayCourtHandler : IRequestHandler<DelayCourtCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    public DelayCourtHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async Task<int> Handle(DelayCourtCommand request, CancellationToken ct)
    {
        var court = await _db.Courts.FirstOrDefaultAsync(c => c.Id == request.CourtId, ct);
        if (court is null) return 0;

        var upcoming = await _db.Matches
            .Where(m => m.CourtId == request.CourtId
                        && m.State == Domain.Enums.MatchState.NotStarted
                        && m.ScheduledStartUtc >= _clock.UtcNow)
            .ToListAsync(ct);

        foreach (var m in upcoming)
        {
            m.Reschedule(m.ScheduledStartUtc.AddMinutes(m.DurationMinutes * request.Slots));
        }
        await _db.SaveChangesAsync(ct);
        return upcoming.Count;
    }
}

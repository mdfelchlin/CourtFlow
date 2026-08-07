using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.Features.Tournaments;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Divisions;

public sealed record DivisionStageHintDto(StageType StageType, StageStatus Status);

public sealed record DivisionDto(
    Guid Id,
    Guid TournamentId,
    string Name,
    DivisionFormat Format,
    int MaxTeams,
    DivisionTiebreakerPreset TiebreakerPreset,
    IReadOnlyList<DivisionStageHintDto> StageHints);

internal static class DivisionMapping
{
    public static DivisionDto ToDto(
        this Division d,
        IReadOnlyList<DivisionStageHintDto> stageHints) =>
        new(d.Id, d.TournamentId, d.Name, d.Format, d.MaxTeams, d.TiebreakerPreset, stageHints);
}

public sealed record ListDivisionsQuery(Guid? TournamentId) : IRequest<IReadOnlyList<DivisionDto>>;

public sealed class ListDivisionsHandler : IRequestHandler<ListDivisionsQuery, IReadOnlyList<DivisionDto>>
{
    private readonly IAppDbContext _db;
    public ListDivisionsHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<DivisionDto>> Handle(ListDivisionsQuery request, CancellationToken ct)
    {
        var q = _db.Divisions.AsNoTracking().AsQueryable();
        if (request.TournamentId is { } t) q = q.Where(d => d.TournamentId == t);
        var items = await q.OrderBy(d => d.Name).ToListAsync(ct);
        if (items.Count == 0)
            return Array.Empty<DivisionDto>();

        var divisionIds = items.Select(d => d.Id).ToList();
        var stages = await _db.Stages.AsNoTracking()
            .Where(s => divisionIds.Contains(s.DivisionId))
            .OrderBy(s => s.StageNumber)
            .ToListAsync(ct);
        var stagesByDivision = stages
            .GroupBy(s => s.DivisionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DivisionStageHintDto>)g
                .Select(s => new DivisionStageHintDto(s.StageType, s.Status))
                .ToList());

        return items.Select(d =>
        {
            var hints = stagesByDivision.TryGetValue(d.Id, out var h) ? h : Array.Empty<DivisionStageHintDto>();
            return d.ToDto(hints);
        }).ToList();
    }
}

public sealed record CreateDivisionCommand(Guid TournamentId, string Name) : IRequest<DivisionDto>;

public sealed class CreateDivisionValidator : AbstractValidator<CreateDivisionCommand>
{
    public CreateDivisionValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
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
        var format = await TournamentQueryHelper.ResolvePlayFormatAsync(_db, request.TournamentId, ct)
                     ?? DivisionFormat.PoolPlusBracket;
        var d = new Division(orgId, request.TournamentId, request.Name, format, maxTeams: 32);
        _db.Divisions.Add(d);
        await DefaultDivisionStages.EnsureProvisionedAsync(_db, d, ct);
        await _db.SaveChangesAsync(ct);
        return d.ToDto(Array.Empty<DivisionStageHintDto>());
    }
}

public sealed record UpdateDivisionCommand(
    Guid Id,
    string Name,
    DivisionFormat Format,
    int MaxTeams,
    DivisionTiebreakerPreset? TiebreakerPreset = null) : IRequest<DivisionDto?>;

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
        if (request.TiebreakerPreset is { } preset)
            d.SetTiebreakerPreset(preset);
        await _db.SaveChangesAsync(ct);

        var stageHints = await _db.Stages.AsNoTracking()
            .Where(s => s.DivisionId == d.Id)
            .OrderBy(s => s.StageNumber)
            .Select(s => new DivisionStageHintDto(s.StageType, s.Status))
            .ToListAsync(ct);

        return d.ToDto(stageHints);
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

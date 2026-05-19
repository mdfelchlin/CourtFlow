using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Stages;

public sealed record StageDto(
    Guid Id,
    Guid DivisionId,
    int StageNumber,
    StageType StageType,
    string Name,
    StageStatus Status,
    string? SeedingSource);

internal static class StageMapper
{
    public static StageDto ToDto(this Stage s) =>
        new(s.Id, s.DivisionId, s.StageNumber, s.StageType, s.Name, s.Status, s.SeedingSource);
}

public sealed record ListStagesQuery(Guid? DivisionId) : IRequest<IReadOnlyList<StageDto>>;

public sealed class ListStagesHandler : IRequestHandler<ListStagesQuery, IReadOnlyList<StageDto>>
{
    private readonly IAppDbContext _db;

    public ListStagesHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<StageDto>> Handle(ListStagesQuery request, CancellationToken ct)
    {
        var q = _db.Stages.AsNoTracking().AsQueryable();
        if (request.DivisionId is { } d)
            q = q.Where(s => s.DivisionId == d);
        var items = await q.OrderBy(s => s.DivisionId).ThenBy(s => s.StageNumber).ToListAsync(ct);
        return items.Select(StageMapper.ToDto).ToList();
    }
}

public sealed record GetStageQuery(Guid Id) : IRequest<StageDto?>;

public sealed class GetStageHandler : IRequestHandler<GetStageQuery, StageDto?>
{
    private readonly IAppDbContext _db;

    public GetStageHandler(IAppDbContext db) => _db = db;

    public async Task<StageDto?> Handle(GetStageQuery request, CancellationToken ct)
    {
        var s = await _db.Stages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return s?.ToDto();
    }
}

public sealed record TransitionStageStatusCommand(Guid StageId, StageStatus TargetStatus) : IRequest<StageDto?>;

public sealed class TransitionStageValidator : AbstractValidator<TransitionStageStatusCommand>
{
    public TransitionStageValidator() => RuleFor(x => x.StageId).NotEmpty();
}

public sealed class TransitionStageHandler : IRequestHandler<TransitionStageStatusCommand, StageDto?>
{
    private readonly IAppDbContext _db;

    public TransitionStageHandler(IAppDbContext db) => _db = db;

    public async Task<StageDto?> Handle(TransitionStageStatusCommand request, CancellationToken ct)
    {
        var s = await _db.Stages.FirstOrDefaultAsync(x => x.Id == request.StageId, ct);
        if (s is null)
            return null;

        s.TransitionTo(request.TargetStatus);
        await _db.SaveChangesAsync(ct);
        return s.ToDto();
    }
}

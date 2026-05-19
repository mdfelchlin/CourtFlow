using System.Text.Json;
using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Divisions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Templates;

public sealed record TemplateStructureSnapshot(
    IReadOnlyList<TemplateDivisionSnapshot> Divisions);

public sealed record TemplateDivisionSnapshot(
    string Name,
    DivisionFormat Format,
    int MaxTeams,
    DivisionTiebreakerPreset TiebreakerPreset,
    IReadOnlyList<DivisionPoolStructureDto> Pools,
    IReadOnlyList<DivisionBracketStructureDto> Brackets,
    IReadOnlyList<DivisionAdvancementRuleDto> AdvancementRules);

public sealed record ListTemplatesQuery() : IRequest<IReadOnlyList<TemplateResultDto>>;

public sealed record GetTemplateQuery(Guid Id) : IRequest<TemplateResultDto?>;

public sealed record DeleteTemplateCommand(Guid Id) : IRequest<bool>;

public sealed record CaptureTemplateCommand(Guid SourceTournamentId, string? Name) : IRequest<TemplateResultDto?>;

public sealed record CreateTournamentFromTemplateCommand(
    Guid TemplateId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes) : IRequest<TournamentFromTemplateResultDto?>;

public sealed record TemplateResultDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? SourceTournamentId,
    int UsageCount,
    DateTime CreatedAt);

public sealed record TournamentFromTemplateResultDto(Guid TournamentId, string Name);

public sealed class ListTemplatesHandler : IRequestHandler<ListTemplatesQuery, IReadOnlyList<TemplateResultDto>>
{
    private readonly IAppDbContext _db;

    public ListTemplatesHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TemplateResultDto>> Handle(ListTemplatesQuery request, CancellationToken ct)
    {
        var items = await _db.TournamentTemplates
            .OrderByDescending(t => t.UpdatedAtUtc)
            .ToListAsync(ct);
        return items.Select(t => t.ToDto()).ToList();
    }
}

public sealed class GetTemplateHandler : IRequestHandler<GetTemplateQuery, TemplateResultDto?>
{
    private readonly IAppDbContext _db;

    public GetTemplateHandler(IAppDbContext db) => _db = db;

    public async Task<TemplateResultDto?> Handle(GetTemplateQuery request, CancellationToken ct)
    {
        var t = await _db.TournamentTemplates.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return t?.ToDto();
    }
}

public sealed class DeleteTemplateHandler : IRequestHandler<DeleteTemplateCommand, bool>
{
    private readonly IAppDbContext _db;

    public DeleteTemplateHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(DeleteTemplateCommand request, CancellationToken ct)
    {
        var t = await _db.TournamentTemplates.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return false;
        _db.TournamentTemplates.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class CaptureTemplateValidator : AbstractValidator<CaptureTemplateCommand>
{
    public CaptureTemplateValidator() => RuleFor(x => x.SourceTournamentId).NotEmpty();
}

public sealed class CreateTournamentFromTemplateValidator : AbstractValidator<CreateTournamentFromTemplateCommand>
{
    public CreateTournamentFromTemplateValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultMatchDurationMinutes).GreaterThan(0);
    }
}

public sealed class CaptureTemplateHandler : IRequestHandler<CaptureTemplateCommand, TemplateResultDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMediator _mediator;

    public CaptureTemplateHandler(IAppDbContext db, ITenantContext tenant, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _mediator = mediator;
    }

    public async Task<TemplateResultDto?> Handle(CaptureTemplateCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var tournament = await _db.Tournaments.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.SourceTournamentId, ct);
        if (tournament is null) return null;

        var divisions = await _db.Divisions.AsNoTracking()
            .Where(d => d.TournamentId == tournament.Id)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        var divisionSnapshots = new List<TemplateDivisionSnapshot>();
        foreach (var div in divisions)
        {
            var wizard = await _mediator.Send(new GetDivisionWizardStateQuery(div.Id), ct);
            if (wizard is null) continue;

            divisionSnapshots.Add(new TemplateDivisionSnapshot(
                div.Name,
                div.Format,
                div.MaxTeams,
                div.TiebreakerPreset,
                wizard.Pools,
                wizard.Brackets,
                wizard.AdvancementRules));
        }

        var json = JsonSerializer.Serialize(new TemplateStructureSnapshot(divisionSnapshots));
        var template = new TournamentTemplate(
            orgId,
            request.Name ?? $"Template from {tournament.Name}",
            request.SourceTournamentId);
        template.SetStructureJson(json);
        _db.TournamentTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return template.ToDto();
    }
}

public sealed class CreateTournamentFromTemplateHandler : IRequestHandler<CreateTournamentFromTemplateCommand, TournamentFromTemplateResultDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMediator _mediator;

    public CreateTournamentFromTemplateHandler(IAppDbContext db, ITenantContext tenant, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _mediator = mediator;
    }

    public async Task<TournamentFromTemplateResultDto?> Handle(CreateTournamentFromTemplateCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var template = await _db.TournamentTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct);
        if (template is null || string.IsNullOrWhiteSpace(template.StructureJson))
            return null;

        var snapshot = JsonSerializer.Deserialize<TemplateStructureSnapshot>(template.StructureJson);
        if (snapshot is null) return null;

        var copy = new Tournament(orgId, request.Name.Trim(), request.StartDate, request.EndDate, request.TimeZoneId,
            request.DefaultMatchDurationMinutes);
        _db.Tournaments.Add(copy);
        _db.ScheduleVersions.Add(new ScheduleVersion(orgId, copy.Id));

        foreach (var divSnap in snapshot.Divisions)
        {
            var div = new Division(orgId, copy.Id, divSnap.Name, divSnap.Format, divSnap.MaxTeams);
            div.SetTiebreakerPreset(divSnap.TiebreakerPreset);
            _db.Divisions.Add(div);
            await DefaultDivisionStages.EnsureProvisionedAsync(_db, div, ct);

            foreach (var pool in divSnap.Pools.OrderBy(p => p.Sequence))
            {
                var np = new Pool(orgId, div.Id, pool.Name);
                np.SetTargetSize(pool.TargetSize);
                np.SetSequence(pool.Sequence);
                _db.Pools.Add(np);
            }

            await _db.SaveChangesAsync(ct);

            var poolsWithIds = await _db.Pools.Where(p => p.DivisionId == div.Id).OrderBy(p => p.Sequence).ToListAsync(ct);
            var poolDtos = divSnap.Pools.Select((p, i) =>
            {
                var entity = poolsWithIds.ElementAtOrDefault(i);
                return new DivisionPoolStructureDto(
                    entity?.Id ?? Guid.Empty,
                    p.Name,
                    p.Sequence,
                    p.TargetSize,
                    p.CourtAllocations);
            }).ToList();

            await _mediator.Send(new SaveDivisionPoolStructureCommand(div.Id, poolDtos), ct);

            if (divSnap.Brackets.Count > 0)
                await _mediator.Send(new SaveDivisionBracketStructureCommand(div.Id, divSnap.Brackets), ct);

            if (divSnap.AdvancementRules.Count > 0)
                await _mediator.Send(new SaveDivisionAdvancementRulesCommand(div.Id, divSnap.AdvancementRules), ct);
        }

        template.IncrementUsage();
        await _db.SaveChangesAsync(ct);

        return new TournamentFromTemplateResultDto(copy.Id, copy.Name);
    }
}

internal static class TemplateMapping
{
    public static TemplateResultDto ToDto(this TournamentTemplate t) =>
        new(t.Id, t.Name, t.Description, t.SourceTournamentId, t.UsageCount, t.CreatedAtUtc);
}

using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Application.Persistence;
using CourtFlow.Application.StageMaterialization;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Divisions;

public sealed record PoolCourtAllocationDto(Guid CourtId, string AllocationType, int PriorityOrder);

public sealed record DivisionPoolStructureDto(
    Guid Id,
    string Name,
    int Sequence,
    int TargetSize,
    IReadOnlyList<PoolCourtAllocationDto> CourtAllocations);

public sealed record DivisionBracketStructureDto(
    Guid? Id,
    string Name,
    BracketType Type,
    int TeamCount,
    bool IfNecessaryFinal,
    int Sequence,
    bool ThirdPlaceMatch = false);

public sealed record DivisionAdvancementRuleDto(
    Guid? Id,
    Guid? SourcePoolId,
    int SourceFinishPosition,
    string TargetBracketName,
    int TargetSeedSlot,
    string OrderBy,
    int Priority);

public sealed record BracketSeedSlotDto(
    Guid Id,
    Guid BracketId,
    string BracketName,
    int SeedPosition,
    string? Label,
    Guid? AdvancementRuleId,
    Guid? ManualTournamentTeamId,
    Guid? ResolvedTournamentTeamId,
    string ExpectedSourceType);

public sealed record DivisionWizardStateDto(
    Guid DivisionId,
    IReadOnlyList<DivisionPoolStructureDto> Pools,
    IReadOnlyList<DivisionBracketStructureDto> Brackets,
    IReadOnlyList<DivisionAdvancementRuleDto> AdvancementRules,
    IReadOnlyList<BracketSeedSlotDto> BracketSeedSlots,
    IReadOnlyList<Guid> AvailableCourtIds);

public sealed record GetDivisionWizardStateQuery(Guid DivisionId) : IRequest<DivisionWizardStateDto?>;

public sealed class GetDivisionWizardStateHandler : IRequestHandler<GetDivisionWizardStateQuery, DivisionWizardStateDto?>
{
    private readonly IAppDbContext _db;

    public GetDivisionWizardStateHandler(IAppDbContext db) => _db = db;

    public async Task<DivisionWizardStateDto?> Handle(GetDivisionWizardStateQuery request, CancellationToken ct)
    {
        var division = await _db.Divisions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var pools = await _db.Pools.AsNoTracking()
            .Where(p => p.DivisionId == division.Id)
            .OrderBy(p => p.Sequence).ThenBy(p => p.Name)
            .ToListAsync(ct);

        var poolIds = pools.Select(p => p.Id).ToList();
        var allocations = await _db.PoolCourtAllocations.AsNoTracking()
            .Where(a => poolIds.Contains(a.PoolId))
            .OrderBy(a => a.PriorityOrder)
            .ToListAsync(ct);

        var brackets = await _db.Brackets.AsNoTracking()
            .Where(b => b.DivisionId == division.Id)
            .OrderBy(b => b.DisplayName)
            .ToListAsync(ct);

        var poolStage = await _db.Stages.AsNoTracking()
            .Where(s => s.DivisionId == division.Id && s.StageType == StageType.Pool)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);

        var rules = poolStage is null
            ? new List<AdvancementRule>()
            : await _db.AdvancementRules.AsNoTracking()
                .Where(r => r.SourceStageId == poolStage.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.TargetSeedSlot)
                .ToListAsync(ct);

        var bracketIds = brackets.Select(b => b.Id).ToList();
        var slots = bracketIds.Count == 0
            ? new List<BracketSeedSlot>()
            : await _db.BracketSeedSlots.AsNoTracking()
                .Where(s => bracketIds.Contains(s.BracketId))
                .OrderBy(s => s.BracketId).ThenBy(s => s.SeedPosition)
                .ToListAsync(ct);

        var courtIds = await _db.Courts.AsNoTracking()
            .Where(c => _db.Venues.Any(v => v.TournamentId == division.TournamentId && v.Id == c.VenueId))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var bracketNameById = brackets.ToDictionary(b => b.Id, b => b.DisplayName ?? "Bracket");

        return new DivisionWizardStateDto(
            division.Id,
            pools.Select(p => new DivisionPoolStructureDto(
                p.Id,
                p.Name,
                p.Sequence,
                p.TargetSize,
                allocations.Where(a => a.PoolId == p.Id)
                    .Select(a => new PoolCourtAllocationDto(a.CourtId, a.AllocationType, a.PriorityOrder))
                    .ToList())).ToList(),
            brackets.Select((b, i) => new DivisionBracketStructureDto(
                b.Id,
                b.DisplayName ?? "Bracket",
                b.Type,
                b.TeamCapacity,
                b.IfNecessaryFinal,
                i,
                b.ThirdPlaceMatch)).ToList(),
            rules.Select(r => new DivisionAdvancementRuleDto(
                r.Id,
                r.SourcePoolId,
                r.SourceFinishPosition,
                bracketNameById.GetValueOrDefault(r.TargetBracketId, "Bracket"),
                r.TargetSeedSlot,
                r.OrderBy,
                r.Priority)).ToList(),
            slots.Select(s => new BracketSeedSlotDto(
                s.Id,
                s.BracketId,
                bracketNameById.GetValueOrDefault(s.BracketId, "Bracket"),
                s.SeedPosition,
                s.Label,
                s.AdvancementRuleId,
                s.ManualTournamentTeamId,
                s.ResolvedTournamentTeamId,
                s.ExpectedSourceType)).ToList(),
            courtIds);
    }
}

public sealed record SaveDivisionPoolStructureCommand(
    Guid DivisionId,
    IReadOnlyList<DivisionPoolStructureDto> Pools) : IRequest<DivisionWizardStateDto?>;

public sealed class SaveDivisionPoolStructureValidator : AbstractValidator<SaveDivisionPoolStructureCommand>
{
    public SaveDivisionPoolStructureValidator()
    {
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.Pools).NotEmpty();
    }
}

public sealed class SaveDivisionPoolStructureHandler : IRequestHandler<SaveDivisionPoolStructureCommand, DivisionWizardStateDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMediator _mediator;

    public SaveDivisionPoolStructureHandler(IAppDbContext db, ITenantContext tenant, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _mediator = mediator;
    }

    public async Task<DivisionWizardStateDto?> Handle(SaveDivisionPoolStructureCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (_db is DbContext dbContext && UsesRelationalBulkDelete(dbContext))
            transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
        var orgId = _tenant.RequireOrganizationId();
        var poolStage = await _db.Stages
            .Where(s => s.DivisionId == division.Id && s.StageType == StageType.Pool)
            .OrderBy(s => s.StageNumber)
            .FirstOrDefaultAsync(ct);

        var canRemovePools = poolStage is null
            || poolStage.Status is StageStatus.Draft or StageStatus.Configured;

        var existingPoolSnapshots = await _db.Pools
            .Where(p => p.DivisionId == division.Id)
            .OrderBy(p => p.Sequence)
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .AsNoTracking()
            .ToListAsync(ct);

        var requested = request.Pools
            .Select((dto, index) => (Dto: dto, Index: index))
            .OrderBy(x => x.Dto.Sequence > 0 ? x.Dto.Sequence : x.Index)
            .ToList();

        var retainedPoolIds = new HashSet<Guid>();

        for (var i = 0; i < requested.Count; i++)
        {
            var (dto, index) = requested[i];
            var sequence = dto.Sequence > 0 ? dto.Sequence : index;

            Pool? pool = null;
            if (dto.Id != Guid.Empty)
                pool = await _db.Pools.FirstOrDefaultAsync(p => p.DivisionId == division.Id && p.Id == dto.Id, ct);

            if (pool is null && dto.Id == Guid.Empty && i < existingPoolSnapshots.Count)
            {
                var candidateId = existingPoolSnapshots[i].Id;
                if (!retainedPoolIds.Contains(candidateId))
                    pool = await _db.Pools.FirstOrDefaultAsync(p => p.Id == candidateId, ct);
            }

            if (pool is null && dto.Id == Guid.Empty)
            {
                var match = existingPoolSnapshots.FirstOrDefault(p =>
                    !retainedPoolIds.Contains(p.Id)
                    && string.Equals(p.Name, dto.Name, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    pool = await _db.Pools.FirstOrDefaultAsync(p => p.Id == match.Id, ct);
            }

            if (pool is null)
            {
                pool = new Pool(orgId, division.Id, dto.Name, sequence, dto.TargetSize);
                _db.Pools.Add(pool);
            }
            else
            {
                pool.Rename(dto.Name);
                pool.SetSequence(sequence);
                pool.SetTargetSize(dto.TargetSize);
            }

            retainedPoolIds.Add(pool.Id);
            await ReplacePoolCourtAllocationsAsync(orgId, pool.Id, dto.CourtAllocations, ct);
        }

        foreach (var staleId in existingPoolSnapshots.Select(p => p.Id).Where(id => !retainedPoolIds.Contains(id)))
        {
            if (!canRemovePools)
                continue;

            if (await PoolHasMaterializedDataAsync(staleId, ct))
                continue;

            await EntityGraphDeletes.DeletePoolIfOrphanedAsync(_db, staleId, ct);
        }

        if (poolStage is not null)
        {
            var stageStatus = await _db.Stages.AsNoTracking()
                .Where(s => s.Id == poolStage.Id)
                .Select(s => s.Status)
                .FirstAsync(ct);
            if (stageStatus == StageStatus.Draft)
                poolStage.TransitionTo(StageStatus.Configured);
        }

        await _db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        return await _mediator.Send(new GetDivisionWizardStateQuery(division.Id), ct);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task ReplacePoolCourtAllocationsAsync(
        Guid orgId,
        Guid poolId,
        IReadOnlyList<PoolCourtAllocationDto> allocations,
        CancellationToken ct)
    {
        await EntityGraphDeletes.ClearPoolCourtAllocationsAsync(_db, poolId, ct);

        foreach (var alloc in allocations)
        {
            _db.PoolCourtAllocations.Add(new PoolCourtAllocation(
                orgId, poolId, alloc.CourtId, alloc.AllocationType, alloc.PriorityOrder));
        }
    }

    private async Task<bool> PoolHasMaterializedDataAsync(Guid poolId, CancellationToken ct) =>
        await _db.PoolEntries.AnyAsync(e => e.PoolId == poolId, ct)
        || await _db.Teams.AnyAsync(t => t.PoolId == poolId, ct)
        || await _db.Matches.AnyAsync(m => m.PoolId == poolId, ct);

    private static bool UsesRelationalBulkDelete(DbContext context)
    {
        var providerName = context.Database.ProviderName ?? string.Empty;
        return providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record SaveDivisionBracketStructureCommand(
    Guid DivisionId,
    IReadOnlyList<DivisionBracketStructureDto> Brackets) : IRequest<DivisionWizardStateDto?>;

public sealed class SaveDivisionBracketStructureHandler : IRequestHandler<SaveDivisionBracketStructureCommand, DivisionWizardStateDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMediator _mediator;

    public SaveDivisionBracketStructureHandler(IAppDbContext db, ITenantContext tenant, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _mediator = mediator;
    }

    public async Task<DivisionWizardStateDto?> Handle(SaveDivisionBracketStructureCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var orgId = _tenant.RequireOrganizationId();
        var existingIds = await _db.Brackets
            .Where(b => b.DivisionId == division.Id)
            .Select(b => b.Id)
            .ToListAsync(ct);

        var keepIds = request.Brackets.Where(b => b.Id is not null).Select(b => b.Id!.Value).ToHashSet();

        foreach (var staleId in existingIds.Where(id => !keepIds.Contains(id)))
        {
            await EntityGraphDeletes.ClearBracketSeedSlotsAsync(_db, staleId, ct);
            await EntityGraphDeletes.DeleteBracketAsync(_db, staleId, ct);
        }

        foreach (var dto in request.Brackets)
        {
            Bracket? bracket = dto.Id is { } id
                ? await _db.Brackets.FirstOrDefaultAsync(b => b.DivisionId == division.Id && b.Id == id, ct)
                : null;

            if (bracket is null)
            {
                bracket = new Bracket(
                    orgId,
                    division.TournamentId,
                    division.Id,
                    dto.Type,
                    dto.TeamCount,
                    dto.IfNecessaryFinal,
                    dto.Name,
                    dto.ThirdPlaceMatch);
                _db.Brackets.Add(bracket);
            }
            else
            {
                bracket.Configure(dto.Type, dto.TeamCount, dto.IfNecessaryFinal, dto.Name, dto.ThirdPlaceMatch);
            }
        }

        await _db.SaveChangesAsync(ct);
        return await _mediator.Send(new GetDivisionWizardStateQuery(division.Id), ct);
    }
}

public sealed record SaveDivisionAdvancementRulesCommand(
    Guid DivisionId,
    IReadOnlyList<DivisionAdvancementRuleDto> Rules) : IRequest<DivisionWizardStateDto?>;

public sealed class SaveDivisionAdvancementRulesHandler : IRequestHandler<SaveDivisionAdvancementRulesCommand, DivisionWizardStateDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMediator _mediator;

    public SaveDivisionAdvancementRulesHandler(IAppDbContext db, ITenantContext tenant, IMediator mediator)
    {
        _db = db;
        _tenant = tenant;
        _mediator = mediator;
    }

    public async Task<DivisionWizardStateDto?> Handle(SaveDivisionAdvancementRulesCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var orgId = _tenant.RequireOrganizationId();
        var (poolStage, _) = await BracketMaterializationHelper.RequireStagesAsync(_db, division.Id, ct);

        var brackets = await _db.Brackets.Where(b => b.DivisionId == division.Id).ToListAsync(ct);
        var bracketByName = brackets.ToDictionary(
            b => b.DisplayName ?? "Bracket",
            b => b,
            StringComparer.OrdinalIgnoreCase);

        await EntityGraphDeletes.ClearAdvancementRulesForStageAsync(_db, poolStage.Id, ct);

        foreach (var dto in request.Rules)
        {
            if (!bracketByName.TryGetValue(dto.TargetBracketName, out var bracket))
                throw new InvalidOperationException($"Bracket '{dto.TargetBracketName}' not found.");

            _db.AdvancementRules.Add(new AdvancementRule(
                orgId,
                poolStage.Id,
                dto.SourceFinishPosition,
                bracket.Id,
                dto.TargetSeedSlot,
                dto.SourcePoolId,
                dto.OrderBy,
                dto.Priority));
        }

        await _db.SaveChangesAsync(ct);
        return await _mediator.Send(new GetDivisionWizardStateQuery(division.Id), ct);
    }
}

public sealed record SetBracketSlotOverrideCommand(
    Guid BracketSeedSlotId,
    Guid? ManualTournamentTeamId,
    string? Reason) : IRequest<BracketSeedSlotDto?>;

public sealed class SetBracketSlotOverrideHandler : IRequestHandler<SetBracketSlotOverrideCommand, BracketSeedSlotDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public SetBracketSlotOverrideHandler(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<BracketSeedSlotDto?> Handle(SetBracketSlotOverrideCommand request, CancellationToken ct)
    {
        var slot = await _db.BracketSeedSlots.FirstOrDefaultAsync(s => s.Id == request.BracketSeedSlotId, ct);
        if (slot is null) return null;

        var orgId = _tenant.RequireOrganizationId();
        var previous = slot.ManualTournamentTeamId;
        slot.SetManualOverride(request.ManualTournamentTeamId);

        _db.BracketSeedSlotOverrides.Add(new BracketSeedSlotOverride(
            orgId,
            slot.Id,
            previous,
            request.ManualTournamentTeamId,
            reason: request.Reason));

        await _db.SaveChangesAsync(ct);

        var bracket = await _db.Brackets.AsNoTracking().FirstAsync(b => b.Id == slot.BracketId, ct);
        return new BracketSeedSlotDto(
            slot.Id,
            slot.BracketId,
            bracket.DisplayName ?? "Bracket",
            slot.SeedPosition,
            slot.Label,
            slot.AdvancementRuleId,
            slot.ManualTournamentTeamId,
            slot.ResolvedTournamentTeamId,
            slot.ExpectedSourceType);
    }
}

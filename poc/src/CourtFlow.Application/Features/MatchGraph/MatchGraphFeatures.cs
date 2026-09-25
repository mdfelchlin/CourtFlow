using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Matches;
using CourtFlow.Application.Matches;
using CourtFlow.Application.Persistence;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.MatchGraph;

public sealed record MatchSlotSourceDto(
    Guid Id,
    Guid MatchId,
    MatchSlotKind Slot,
    MatchSlotSourceKind SourceKind,
    Guid? FixedTournamentTeamId,
    Guid? SourceMatchId,
    Guid? SourcePoolId,
    int? SourcePosition,
    Guid? ResolvedTournamentTeamId);

public sealed record DivisionMatchGraphMatchDto(
    MatchDto Match,
    IReadOnlyList<MatchSlotSourceDto> SlotSources,
    int DisplayNumber);

public sealed record DivisionMatchGraphDto(
    Guid DivisionId,
    IReadOnlyList<DivisionMatchGraphMatchDto> Matches);

public sealed record GetDivisionMatchGraphQuery(Guid DivisionId) : IRequest<DivisionMatchGraphDto?>;

public sealed class GetDivisionMatchGraphHandler : IRequestHandler<GetDivisionMatchGraphQuery, DivisionMatchGraphDto?>
{
    private readonly IAppDbContext _db;
    public GetDivisionMatchGraphHandler(IAppDbContext db) => _db = db;

    public async Task<DivisionMatchGraphDto?> Handle(GetDivisionMatchGraphQuery request, CancellationToken ct)
    {
        var division = await _db.Divisions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var matches = await _db.Matches
            .Include(m => m.Sets)
            .Where(m => m.DivisionId == request.DivisionId)
            .OrderBy(m => m.ScheduledStartUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);

        var matchIds = matches.Select(m => m.Id).ToList();
        var sources = await _db.MatchSlotSources
            .Where(s => matchIds.Contains(s.MatchId))
            .ToListAsync(ct);
        var sourcesByMatch = sources.GroupBy(s => s.MatchId).ToDictionary(g => g.Key, g => g.ToList());

        var result = matches.Select((m, i) => new DivisionMatchGraphMatchDto(
            m.ToDto(),
            (sourcesByMatch.GetValueOrDefault(m.Id) ?? [])
                .OrderBy(s => s.Slot)
                .Select(s => new MatchSlotSourceDto(
                    s.Id, s.MatchId, s.Slot, s.SourceKind,
                    s.FixedTournamentTeamId, s.SourceMatchId, s.SourcePoolId,
                    s.SourcePosition, s.ResolvedTournamentTeamId))
                .ToList(),
            i + 1)).ToList();

        return new DivisionMatchGraphDto(request.DivisionId, result);
    }
}

public sealed record CreateDivisionMatchCommand(
    Guid DivisionId,
    Guid? StageId,
    Guid? PoolId,
    Guid? CourtId,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    Guid? HomeTeamId,
    Guid? AwayTeamId,
    Guid? RefTeamId) : IRequest<MatchDto?>;

public sealed class CreateDivisionMatchValidator : AbstractValidator<CreateDivisionMatchCommand>
{
    public CreateDivisionMatchValidator()
    {
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
    }
}

public sealed class CreateDivisionMatchHandler : IRequestHandler<CreateDivisionMatchCommand, MatchDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public CreateDivisionMatchHandler(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<MatchDto?> Handle(CreateDivisionMatchCommand request, CancellationToken ct)
    {
        var division = await _db.Divisions.FirstOrDefaultAsync(d => d.Id == request.DivisionId, ct);
        if (division is null) return null;

        var orgId = _tenant.RequireOrganizationId();
        var wave = await _db.Teams.Where(t => t.DivisionId == request.DivisionId)
            .Select(t => t.Wave)
            .FirstOrDefaultAsync(ct);

        var match = new Match(
            orgId,
            division.TournamentId,
            division.Id,
            wave,
            request.ScheduledStartUtc,
            request.DurationMinutes,
            poolId: request.PoolId,
            courtId: request.CourtId,
            homeTeamId: request.HomeTeamId,
            awayTeamId: request.AwayTeamId,
            refTeamId: request.RefTeamId,
            stageId: request.StageId);

        _db.Matches.Add(match);
        await SyncFixedSlotSourcesAsync(orgId, match, request.HomeTeamId, request.AwayTeamId, request.RefTeamId, ct);
        await _db.SaveChangesAsync(ct);
        return match.ToDto();
    }

    private async Task SyncFixedSlotSourcesAsync(
        Guid orgId,
        Match match,
        Guid? homeTeamId,
        Guid? awayTeamId,
        Guid? refTeamId,
        CancellationToken ct)
    {
        if (homeTeamId is { } home)
            await EnsureFixedSlotAsync(orgId, match.Id, MatchSlotKind.Home, home, match.DivisionId, ct);
        if (awayTeamId is { } away)
            await EnsureFixedSlotAsync(orgId, match.Id, MatchSlotKind.Away, away, match.DivisionId, ct);
        if (refTeamId is { } refTeam)
            await EnsureFixedSlotAsync(orgId, match.Id, MatchSlotKind.Ref, refTeam, match.DivisionId, ct);
    }

    private async Task EnsureFixedSlotAsync(
        Guid orgId,
        Guid matchId,
        MatchSlotKind slot,
        Guid teamId,
        Guid divisionId,
        CancellationToken ct)
    {
        var ttId = await _db.TournamentTeams
            .Where(tt => tt.DivisionId == divisionId && tt.TeamId == teamId)
            .Select(tt => (Guid?)tt.Id)
            .FirstOrDefaultAsync(ct);
        if (ttId is null) return;

        var existing = await _db.MatchSlotSources
            .FirstOrDefaultAsync(s => s.MatchId == matchId && s.Slot == slot, ct);
        if (existing is null)
        {
            existing = new MatchSlotSource(orgId, matchId, slot, MatchSlotSourceKind.FixedTeam);
            _db.MatchSlotSources.Add(existing);
        }

        existing.ConfigureFixedTeam(ttId.Value);
    }
}

public sealed record DeleteDivisionMatchCommand(Guid MatchId) : IRequest<bool>;

public sealed class DeleteDivisionMatchHandler : IRequestHandler<DeleteDivisionMatchCommand, bool>
{
    private readonly IAppDbContext _db;
    public DeleteDivisionMatchHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(DeleteDivisionMatchCommand request, CancellationToken ct)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == request.MatchId, ct);
        if (match is null) return false;
        if (match.State is MatchState.InProgress or MatchState.Final)
            throw new InvalidOperationException("Cannot delete a match that is in progress or final.");

        if (match.BracketNodeId is not null)
            throw new InvalidOperationException("Cannot delete bracket-managed matches here — adjust bracket structure instead.");

        var referenced = await _db.MatchSlotSources
            .AnyAsync(s => s.SourceMatchId == match.Id, ct);
        if (referenced)
            throw new InvalidOperationException("Remove match connections that reference this game first.");

        await EntityGraphDeletes.ClearMatchSlotSourcesAsync(_db, match.Id, ct);
        _db.Matches.Remove(match);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record SetMatchSlotSourceCommand(
    Guid MatchId,
    MatchSlotKind Slot,
    MatchSlotSourceKind SourceKind,
    Guid? FixedTournamentTeamId,
    Guid? SourceMatchId,
    Guid? SourcePoolId,
    int? SourcePosition) : IRequest<MatchSlotSourceDto?>;

public sealed class SetMatchSlotSourceHandler : IRequestHandler<SetMatchSlotSourceCommand, MatchSlotSourceDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IMatchProgressionService _progression;

    public SetMatchSlotSourceHandler(IAppDbContext db, ITenantContext tenant, IMatchProgressionService progression)
    {
        _db = db;
        _tenant = tenant;
        _progression = progression;
    }

    public async Task<MatchSlotSourceDto?> Handle(SetMatchSlotSourceCommand request, CancellationToken ct)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == request.MatchId, ct);
        if (match is null) return null;

        var orgId = _tenant.RequireOrganizationId();
        var slot = await _db.MatchSlotSources
            .FirstOrDefaultAsync(s => s.MatchId == request.MatchId && s.Slot == request.Slot, ct);

        if (slot is null)
        {
            slot = new MatchSlotSource(orgId, request.MatchId, request.Slot, request.SourceKind);
            _db.MatchSlotSources.Add(slot);
        }

        switch (request.SourceKind)
        {
            case MatchSlotSourceKind.FixedTeam:
                if (request.FixedTournamentTeamId is not { } tt)
                    throw new InvalidOperationException("FixedTournamentTeamId is required for FixedTeam sources.");
                slot.ConfigureFixedTeam(tt);
                break;
            case MatchSlotSourceKind.MatchWinner:
            case MatchSlotSourceKind.MatchLoser:
                if (request.SourceMatchId is not { } src)
                    throw new InvalidOperationException("SourceMatchId is required for match outcome sources.");
                slot.ConfigureMatchOutcome(src, request.SourceKind);
                break;
            case MatchSlotSourceKind.PoolFinish:
                if (request.SourcePoolId is not { } poolId || request.SourcePosition is not { } finish)
                    throw new InvalidOperationException("SourcePoolId and SourcePosition are required for PoolFinish.");
                slot.ConfigurePoolFinish(poolId, finish);
                break;
            case MatchSlotSourceKind.Ranking:
                if (request.SourcePosition is not { } rank)
                    throw new InvalidOperationException("SourcePosition (rank) is required for Ranking sources.");
                slot.ConfigureRanking(rank);
                break;
        }

        await _db.SaveChangesAsync(ct);

        if (request.SourceKind is MatchSlotSourceKind.PoolFinish or MatchSlotSourceKind.Ranking)
            await _progression.ResolveStandingsSourcesAsync(match.DivisionId, ct);
        else if (request.SourceKind is MatchSlotSourceKind.MatchWinner or MatchSlotSourceKind.MatchLoser
                 && request.SourceMatchId is { } finalizedId)
        {
            var source = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == finalizedId, ct);
            if (source?.State == MatchState.Final)
                await _progression.OnMatchFinalizedAsync(finalizedId, ct);
        }
        else if (request.SourceKind == MatchSlotSourceKind.FixedTeam)
        {
            var teamId = await _db.TournamentTeams.AsNoTracking()
                .Where(tt => tt.Id == request.FixedTournamentTeamId)
                .Select(tt => (Guid?)tt.TeamId)
                .FirstOrDefaultAsync(ct);
            switch (request.Slot)
            {
                case MatchSlotKind.Home: match.AssignTeams(teamId, match.AwayTeamId); break;
                case MatchSlotKind.Away: match.AssignTeams(match.HomeTeamId, teamId); break;
                case MatchSlotKind.Ref: match.AssignReferee(teamId); break;
            }
            await _db.SaveChangesAsync(ct);
        }

        return new MatchSlotSourceDto(
            slot.Id, slot.MatchId, slot.Slot, slot.SourceKind,
            slot.FixedTournamentTeamId, slot.SourceMatchId, slot.SourcePoolId,
            slot.SourcePosition, slot.ResolvedTournamentTeamId);
    }
}

public sealed record ResolveDivisionMatchSourcesCommand(Guid DivisionId) : IRequest;

public sealed class ResolveDivisionMatchSourcesHandler : IRequestHandler<ResolveDivisionMatchSourcesCommand>
{
    private readonly IMatchProgressionService _progression;
    public ResolveDivisionMatchSourcesHandler(IMatchProgressionService progression) => _progression = progression;

    public Task Handle(ResolveDivisionMatchSourcesCommand request, CancellationToken ct) =>
        _progression.ResolveStandingsSourcesAsync(request.DivisionId, ct);
}

public sealed record GetDivisionValidationQuery(Guid DivisionId) : IRequest<ReadModels.DivisionValidation.DivisionValidationResultDto?>;

public sealed class GetDivisionValidationHandler
    : IRequestHandler<GetDivisionValidationQuery, ReadModels.DivisionValidation.DivisionValidationResultDto?>
{
    private readonly ReadModels.DivisionValidation.IDivisionValidation _validation;
    private readonly IAppDbContext _db;

    public GetDivisionValidationHandler(ReadModels.DivisionValidation.IDivisionValidation validation, IAppDbContext db)
    {
        _validation = validation;
        _db = db;
    }

    public async Task<ReadModels.DivisionValidation.DivisionValidationResultDto?> Handle(
        GetDivisionValidationQuery request, CancellationToken ct)
    {
        var exists = await _db.Divisions.AnyAsync(d => d.Id == request.DivisionId, ct);
        if (!exists) return null;
        return await _validation.EvaluateAsync(request.DivisionId, ct);
    }
}

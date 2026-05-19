using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Matches;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.ScheduleVersions;

public sealed record ScheduleVersionSummaryDto(
    Guid Id,
    ScheduleVersionStatus Status,
    long MutationSequence,
    DateTime? PublishedAt,
    bool IsWorking);

public sealed record PreviewScheduleMoveResult(
    Guid ScheduleVersionId,
    long MutationSequence,
    ConflictReport Conflicts,
    string ApplyToken,
    bool CanApply);

public sealed record ListScheduleVersionsQuery(Guid TournamentId) : IRequest<IReadOnlyList<ScheduleVersionSummaryDto>>;

public sealed record PublishScheduleVersionCommand(Guid TournamentId) : IRequest<ScheduleVersionSummaryDto?>;

public sealed record PreviewScheduleMoveCommand(
    Guid TournamentId,
    Guid MatchId,
    Guid? CourtId,
    DateTime ScheduledStartUtc,
    int? DurationMinutes) : IRequest<PreviewScheduleMoveResult?>;

public sealed record ApplyScheduleMoveCommand(Guid TournamentId, string ApplyToken) : IRequest<MatchDto?>;

public sealed class PreviewScheduleMoveValidator : AbstractValidator<PreviewScheduleMoveCommand>
{
    public PreviewScheduleMoveValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.MatchId).NotEmpty();
    }
}

public sealed class ApplyScheduleMoveValidator : AbstractValidator<ApplyScheduleMoveCommand>
{
    public ApplyScheduleMoveValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.ApplyToken).NotEmpty();
    }
}

public sealed class ListScheduleVersionsHandler : IRequestHandler<ListScheduleVersionsQuery, IReadOnlyList<ScheduleVersionSummaryDto>>
{
    private readonly IAppDbContext _db;

    public ListScheduleVersionsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScheduleVersionSummaryDto>> Handle(ListScheduleVersionsQuery request, CancellationToken ct)
    {
        var workingId = (await ScheduleVersionProvisioning.GetLatestNonSupersededAsync(_db, request.TournamentId, ct))?.Id;
        var versions = await _db.ScheduleVersions
            .AsNoTracking()
            .Where(v => v.TournamentId == request.TournamentId)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync(ct);

        return versions.Select(v => new ScheduleVersionSummaryDto(
            v.Id,
            v.Status,
            v.MutationSequence,
            v.PublishedAt,
            v.Id == workingId)).ToList();
    }
}

public sealed class PublishScheduleVersionHandler : IRequestHandler<PublishScheduleVersionCommand, ScheduleVersionSummaryDto?>
{
    private readonly IAppDbContext _db;

    public PublishScheduleVersionHandler(IAppDbContext db) => _db = db;

    public async Task<ScheduleVersionSummaryDto?> Handle(PublishScheduleVersionCommand request, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == request.TournamentId, ct);
        if (tournament is null) return null;

        var published = await ScheduleVersionProvisioning.PublishAsync(_db, request.TournamentId, ct);
        return new ScheduleVersionSummaryDto(
            published.Id,
            published.Status,
            published.MutationSequence,
            published.PublishedAt,
            IsWorking: true);
    }
}

public sealed class PreviewScheduleMoveHandler : IRequestHandler<PreviewScheduleMoveCommand, PreviewScheduleMoveResult?>
{
    private readonly IAppDbContext _db;
    private readonly IConstraintChecker _checker;
    private readonly IScheduleApplyTokenIssuer _issuer;

    public PreviewScheduleMoveHandler(IAppDbContext db, IConstraintChecker checker, IScheduleApplyTokenIssuer issuer)
    {
        _db = db;
        _checker = checker;
        _issuer = issuer;
    }

    public async Task<PreviewScheduleMoveResult?> Handle(PreviewScheduleMoveCommand request, CancellationToken ct)
    {
        var match = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MatchId, ct);
        if (match is null || match.TournamentId != request.TournamentId)
            return null;

        var dur = request.DurationMinutes ?? match.DurationMinutes;
        var version = await ScheduleVersionProvisioning.EnsureWorkingForMutationAsync(_db, request.TournamentId, ct);

        var report = await _checker.CheckHypotheticalMoveAsync(
            request.TournamentId,
            request.MatchId,
            request.CourtId,
            request.ScheduledStartUtc,
            dur,
            ct);

        string token = "";
        var canApply = !report.HasConflicts;
        if (canApply)
        {
            var payload = new ScheduleApplyPayload(
                version.Id,
                version.MutationSequence,
                request.MatchId,
                request.CourtId,
                request.ScheduledStartUtc,
                dur);
            token = _issuer.Sign(payload);
        }

        return new PreviewScheduleMoveResult(version.Id, version.MutationSequence, report, token, canApply);
    }
}

public sealed class ApplyScheduleMoveHandler : IRequestHandler<ApplyScheduleMoveCommand, MatchDto?>
{
    private readonly IAppDbContext _db;
    private readonly IConstraintChecker _checker;
    private readonly IScheduleApplyTokenIssuer _issuer;

    public ApplyScheduleMoveHandler(IAppDbContext db, IConstraintChecker checker, IScheduleApplyTokenIssuer issuer)
    {
        _db = db;
        _checker = checker;
        _issuer = issuer;
    }

    public async Task<MatchDto?> Handle(ApplyScheduleMoveCommand request, CancellationToken ct)
    {
        if (!_issuer.TryVerify(request.ApplyToken, out var envelope) || envelope is null)
            throw new ScheduleApplyTokenInvalidException();

        var version = await _db.ScheduleVersions.FirstOrDefaultAsync(v => v.Id == envelope.ScheduleVersionId, ct);
        if (version is null || version.TournamentId != request.TournamentId)
            throw new ScheduleApplyTokenInvalidException();

        if (version.MutationSequence != envelope.MutationSequence)
            throw new StaleScheduleApplyException();

        var match = await _db.Matches.Include(m => m.Sets).FirstOrDefaultAsync(m => m.Id == envelope.MatchId, ct);
        if (match is null || match.TournamentId != request.TournamentId || match.Id != envelope.MatchId)
            throw new ScheduleApplyTokenInvalidException();

        var recheck = await _checker.CheckHypotheticalMoveAsync(
            request.TournamentId,
            envelope.MatchId,
            envelope.CourtId,
            envelope.ScheduledStartUtc,
            envelope.DurationMinutes,
            ct);

        if (recheck.HasConflicts)
            throw new ScheduleConflictAfterPreviewException();

        version = await ScheduleVersionProvisioning.EnsureWorkingForMutationAsync(_db, request.TournamentId, ct);
        if (version.Id != envelope.ScheduleVersionId)
            throw new StaleScheduleApplyException();

        match.AssignCourt(envelope.CourtId);
        match.Reschedule(envelope.ScheduledStartUtc, envelope.DurationMinutes);

        if (envelope.CourtId is { } courtId)
        {
            var end = envelope.ScheduledStartUtc.AddMinutes(envelope.DurationMinutes);
            var assignment = await _db.MatchAssignments
                .FirstOrDefaultAsync(a => a.ScheduleVersionId == version.Id && a.MatchId == match.Id, ct);

            if (assignment is null)
            {
                _db.MatchAssignments.Add(new MatchAssignment(
                    match.OrganizationId,
                    version.Id,
                    match.Id,
                    courtId,
                    envelope.ScheduledStartUtc,
                    end));
            }
            else
            {
                assignment.Reassign(courtId, envelope.ScheduledStartUtc, end);
            }
        }

        version.BumpAfterCommittedApply();
        await _db.SaveChangesAsync(ct);
        return match.ToDto();
    }
}

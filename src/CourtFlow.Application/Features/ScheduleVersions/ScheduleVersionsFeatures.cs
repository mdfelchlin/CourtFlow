using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Matches;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.ScheduleVersions;

public sealed record ScheduleVersionSummaryDto(Guid Id, long MutationSequence);

public sealed record PreviewScheduleMoveResult(
    Guid ScheduleVersionId,
    long MutationSequence,
    ConflictReport Conflicts,
    string ApplyToken,
    bool CanApply);

public sealed record ListScheduleVersionsQuery(Guid TournamentId) : IRequest<IReadOnlyList<ScheduleVersionSummaryDto>>;

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
        var version = await ScheduleVersionProvisioning.EnsurePublishedAsync(_db, request.TournamentId, ct);
        return new[] { new ScheduleVersionSummaryDto(version.Id, version.MutationSequence) };
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
        var version = await ScheduleVersionProvisioning.EnsurePublishedAsync(_db, request.TournamentId, ct);

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

        match.AssignCourt(envelope.CourtId);
        match.Reschedule(envelope.ScheduledStartUtc, envelope.DurationMinutes);
        version.BumpAfterCommittedApply();

        await _db.SaveChangesAsync(ct);
        return match.ToDto();
    }
}

internal static class ScheduleVersionProvisioning
{
    public static async Task<ScheduleVersion> EnsurePublishedAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct)
    {
        var existing = await db.ScheduleVersions.FirstOrDefaultAsync(s => s.TournamentId == tournamentId, ct);
        if (existing is not null)
            return existing;

        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
                         ?? throw new InvalidOperationException("Tournament not found.");

        var created = new ScheduleVersion(tournament.OrganizationId, tournamentId);
        db.ScheduleVersions.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}

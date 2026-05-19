using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Application.Reflow;
using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CourtFlow.Infrastructure.Reflow;

/// <summary>Preview and apply schedule reflow by shifting delayed court assignments forward.</summary>
public sealed class ReflowPreviewEngine : IReflowPreviewEngine
{
    private const int MinDelayMinutes = 10;

    private readonly IAppDbContext _db;
    private readonly IReflowApplyTokenIssuer _issuer;

    public ReflowPreviewEngine(IAppDbContext db, IReflowApplyTokenIssuer issuer)
    {
        _db = db;
        _issuer = issuer;
    }

    public async Task<ReflowPreviewDelta> PreviewAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var version = await _db.ScheduleVersions
            .AsNoTracking()
            .Where(v => v.TournamentId == tournamentId && v.Status != ScheduleVersionStatus.Superseded)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (version is null)
            return new ReflowPreviewDelta(new[] { "No schedule version to reflow." }, Array.Empty<ReflowAssignmentMove>(), null);

        var moves = await ComputeMovesAsync(version, ct);
        var notes = BuildNotes(moves);

        if (moves.Count == 0)
            return new ReflowPreviewDelta(notes, moves, null);

        var movePayloads = new List<ReflowMovePayload>();
        foreach (var move in moves)
        {
            var duration = await GetMatchDurationAsync(move.MatchId, ct);
            movePayloads.Add(new ReflowMovePayload(
                move.MatchAssignmentId,
                move.MatchId,
                move.NewScheduledStart,
                move.NewScheduledStart.AddMinutes(duration)));
        }

        var payload = new ReflowApplyPayload(
            tournamentId,
            version.Id,
            version.MutationSequence,
            movePayloads);

        return new ReflowPreviewDelta(notes, moves, _issuer.Sign(payload));
    }

    public async Task<ReflowApplyResult> ApplyAsync(
        Guid tournamentId,
        string applyToken,
        string? createdByUserId,
        CancellationToken ct = default)
    {
        if (!_issuer.TryVerify(applyToken, out var envelope) || envelope is null || envelope.TournamentId != tournamentId)
            throw new ScheduleApplyTokenInvalidException();

        var version = await _db.ScheduleVersions.FirstOrDefaultAsync(v => v.Id == envelope.ScheduleVersionId, ct);
        if (version is null || version.TournamentId != tournamentId)
            throw new ScheduleApplyTokenInvalidException();

        if (version.MutationSequence != envelope.MutationSequence)
            throw new StaleScheduleApplyException();

        version = await ScheduleVersionProvisioning.EnsureWorkingForMutationAsync(_db, tournamentId, ct);
        if (version.Id != envelope.ScheduleVersionId)
            throw new StaleScheduleApplyException();

        var orgId = version.OrganizationId;
        var job = new RepairJob(orgId, tournamentId, RepairTriggerType.ScheduleReflow, createdByUserId);
        var diagnostics = JsonSerializer.Serialize(new
        {
            Moves = envelope.Moves.Count,
            Summary = $"{envelope.Moves.Count} assignment(s) shifted"
        });

        var updated = 0;
        foreach (var move in envelope.Moves)
        {
            var assignment = await _db.MatchAssignments
                .FirstOrDefaultAsync(a => a.Id == move.MatchAssignmentId && a.ScheduleVersionId == version.Id, ct);
            if (assignment is null) continue;

            var beforeJson = JsonSerializer.Serialize(new
            {
                assignment.CourtId,
                assignment.ScheduledStart,
                assignment.ScheduledEnd
            });

            assignment.Reassign(assignment.CourtId, move.NewScheduledStart, move.NewScheduledEnd);

            var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == move.MatchId, ct);
            if (match is not null)
            {
                var duration = (int)(move.NewScheduledEnd - move.NewScheduledStart).TotalMinutes;
                match.Reschedule(move.NewScheduledStart, duration);
            }

            job.AddAction(
                RepairActionType.MatchAssignmentMove,
                move.MatchId.ToString(),
                beforeJson,
                JsonSerializer.Serialize(new { move.NewScheduledStart, move.NewScheduledEnd }));

            updated++;
        }

        version.BumpAfterCommittedApply();
        job.MarkApplied(diagnostics);
        _db.RepairJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        return new ReflowApplyResult(job.Id, updated);
    }

    private async Task<IReadOnlyList<ReflowAssignmentMove>> ComputeMovesAsync(ScheduleVersion version, CancellationToken ct)
    {
        var assignments = await _db.MatchAssignments
            .AsNoTracking()
            .Where(a => a.ScheduleVersionId == version.Id)
            .OrderBy(a => a.CourtId)
            .ThenBy(a => a.ScheduledStart)
            .ToListAsync(ct);

        if (assignments.Count == 0)
            return Array.Empty<ReflowAssignmentMove>();

        var matchIds = assignments.Select(a => a.MatchId).Distinct().ToList();
        var matches = await _db.Matches
            .AsNoTracking()
            .Where(m => matchIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var moves = new List<ReflowAssignmentMove>();
        var now = DateTime.UtcNow;

        foreach (var group in assignments.GroupBy(a => a.CourtId))
        {
            var runningShift = 0;
            foreach (var assignment in group.OrderBy(a => a.ScheduledStart))
            {
                if (!matches.TryGetValue(assignment.MatchId, out var match))
                    continue;

                if (match.State is MatchState.InProgress or MatchState.Final)
                {
                    var scheduledEnd = assignment.ScheduledEnd;
                    var actualAnchor = match.LastScoreAtUtc ?? match.ActualStartUtc;
                    if (actualAnchor is not null)
                    {
                        var delay = (int)Math.Max(0, (actualAnchor.Value - scheduledEnd).TotalMinutes);
                        if (delay >= MinDelayMinutes)
                            runningShift = Math.Max(runningShift, delay);
                    }
                    else if (match.State == MatchState.InProgress && assignment.ScheduledStart < now)
                    {
                        var delay = (int)Math.Max(0, (now - scheduledEnd).TotalMinutes);
                        if (delay >= MinDelayMinutes)
                            runningShift = Math.Max(runningShift, delay);
                    }
                }
                else if (match.State == MatchState.NotStarted && runningShift >= MinDelayMinutes)
                {
                    var newStart = assignment.ScheduledStart.AddMinutes(runningShift);
                    if (newStart != assignment.ScheduledStart)
                    {
                        moves.Add(new ReflowAssignmentMove(
                            assignment.Id,
                            assignment.MatchId,
                            assignment.ScheduledStart,
                            newStart,
                            runningShift));
                    }
                }
            }
        }

        return moves;
    }

    private async Task<int> GetMatchDurationAsync(Guid matchId, CancellationToken ct)
    {
        var match = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == matchId, ct);
        return match?.DurationMinutes ?? 60;
    }

    private static IReadOnlyList<string> BuildNotes(IReadOnlyList<ReflowAssignmentMove> moves)
    {
        if (moves.Count == 0)
            return new[] { "No reflow moves suggested — courts appear on schedule." };

        var notes = new List<string> { $"{moves.Count} assignment(s) will shift forward to absorb court delay." };
        foreach (var move in moves.Take(8))
            notes.Add($"Match {move.MatchId.ToString()[..8]}… +{move.ShiftMinutes} min → {move.NewScheduledStart:HH:mm} UTC");
        if (moves.Count > 8)
            notes.Add($"+ {moves.Count - 8} more…");
        return notes;
    }
}

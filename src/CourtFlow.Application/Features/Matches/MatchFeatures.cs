using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Application.Features.ScheduleVersions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Matches;

public sealed record MatchSetDto(int SetNumber, int HomeScore, int AwayScore);

public sealed record MatchDto(
    Guid Id,
    Guid TournamentId,
    Guid DivisionId,
    Guid? PoolId,
    Guid? CourtId,
    Guid? HomeTeamId,
    Guid? AwayTeamId,
    Guid? RefTeamId,
    Wave Wave,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    DateTime? ActualStartUtc,
    DateTime? LastScoreAtUtc,
    MatchState State,
    Guid? WinnerTeamId,
    Guid? BracketNodeId,
    IReadOnlyList<MatchSetDto> Sets);

internal static class MatchMapping
{
    public static MatchDto ToDto(this Match m, MatchAssignment? assignment = null) => new(
        m.Id, m.TournamentId, m.DivisionId, m.PoolId,
        assignment?.CourtId ?? m.CourtId,
        m.HomeTeamId, m.AwayTeamId, m.RefTeamId, m.Wave,
        assignment?.ScheduledStart ?? m.ScheduledStartUtc,
        assignment is null
            ? m.DurationMinutes
            : (int)(assignment.ScheduledEnd - assignment.ScheduledStart).TotalMinutes,
        m.ActualStartUtc, m.LastScoreAtUtc,
        m.State, m.WinnerTeamId, m.BracketNodeId,
        m.Sets.OrderBy(s => s.SetNumber).Select(s => new MatchSetDto(s.SetNumber, s.HomeScore, s.AwayScore)).ToList());

    public static MatchDto ToDto(this Match m) => ToDto(m, assignment: null);
}

public sealed record ListMatchesQuery(Guid? TournamentId, Guid? DivisionId) : IRequest<IReadOnlyList<MatchDto>>;

public sealed class ListMatchesHandler : IRequestHandler<ListMatchesQuery, IReadOnlyList<MatchDto>>
{
    private readonly IAppDbContext _db;
    public ListMatchesHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<MatchDto>> Handle(ListMatchesQuery request, CancellationToken ct)
    {
        var q = _db.Matches.Include(m => m.Sets).AsQueryable();
        if (request.TournamentId is { } t) q = q.Where(m => m.TournamentId == t);
        if (request.DivisionId is { } d) q = q.Where(m => m.DivisionId == d);
        var items = await q.OrderBy(m => m.ScheduledStartUtc).ToListAsync(ct);

        Dictionary<Guid, MatchAssignment>? assignmentByMatch = null;
        if (request.TournamentId is { } tournamentId)
        {
            var version = await ScheduleVersionProvisioning.GetLatestNonSupersededAsync(_db, tournamentId, ct);
            if (version is not null)
            {
                var assignments = await _db.MatchAssignments
                    .Where(a => a.ScheduleVersionId == version.Id)
                    .ToListAsync(ct);
                assignmentByMatch = assignments.ToDictionary(a => a.MatchId);
            }
        }

        return items.Select(m =>
            assignmentByMatch is not null && assignmentByMatch.TryGetValue(m.Id, out var assignment)
                ? m.ToDto(assignment)
                : m.ToDto()).ToList();
    }
}

public sealed record GetMatchQuery(Guid Id) : IRequest<MatchDto?>;

public sealed class GetMatchHandler : IRequestHandler<GetMatchQuery, MatchDto?>
{
    private readonly IAppDbContext _db;
    public GetMatchHandler(IAppDbContext db) => _db = db;
    public async Task<MatchDto?> Handle(GetMatchQuery request, CancellationToken ct)
    {
        var m = await _db.Matches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return m?.ToDto();
    }
}

public sealed record MoveMatchCommand(Guid Id, Guid? CourtId, DateTime ScheduledStartUtc, int? DurationMinutes) : IRequest<MatchDto?>;

public sealed class MoveMatchHandler : IRequestHandler<MoveMatchCommand, MatchDto?>
{
    private readonly IAppDbContext _db;
    public MoveMatchHandler(IAppDbContext db) => _db = db;
    public async Task<MatchDto?> Handle(MoveMatchCommand request, CancellationToken ct)
    {
        var m = await _db.Matches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (m is null) return null;
        m.AssignCourt(request.CourtId);
        m.Reschedule(request.ScheduledStartUtc, request.DurationMinutes);
        await _db.SaveChangesAsync(ct);
        return m.ToDto();
    }
}

public sealed record ScoreMatchCommand(Guid Id, IReadOnlyList<MatchSetDto> Sets, bool Finalize, Guid? WinnerTeamId) : IRequest<MatchDto?>;

public sealed class ScoreMatchValidator : AbstractValidator<ScoreMatchCommand>
{
    public ScoreMatchValidator()
    {
        RuleFor(x => x.Sets).NotEmpty();
        RuleForEach(x => x.Sets).ChildRules(set =>
        {
            set.RuleFor(s => s.SetNumber).GreaterThan(0);
            set.RuleFor(s => s.HomeScore).GreaterThanOrEqualTo(0);
            set.RuleFor(s => s.AwayScore).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class ScoreMatchHandler : IRequestHandler<ScoreMatchCommand, MatchDto?>
{
    private readonly IAppDbContext _db;
    private readonly IBracketProgressionService _progression;

    public ScoreMatchHandler(IAppDbContext db, IBracketProgressionService progression)
    {
        _db = db;
        _progression = progression;
    }

    public async Task<MatchDto?> Handle(ScoreMatchCommand request, CancellationToken ct)
    {
        var m = await _db.Matches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (m is null) return null;

        foreach (var s in request.Sets)
        {
            m.ReportSet(s.SetNumber, s.HomeScore, s.AwayScore);
        }

        if (request.Finalize)
        {
            var winner = request.WinnerTeamId ?? InferWinner(m);
            m.Finalize(winner);
        }

        await _db.SaveChangesAsync(ct);

        if (request.Finalize && m.BracketNodeId is not null)
        {
            await _progression.AdvanceAsync(m.Id, ct);
        }

        return m.ToDto();
    }

    private static Guid? InferWinner(Match match)
    {
        var sets = match.Sets;
        if (sets.Count == 0) return null;
        var homeSetWins = sets.Count(s => s.HomeScore > s.AwayScore);
        var awaySetWins = sets.Count(s => s.AwayScore > s.HomeScore);
        if (homeSetWins == awaySetWins) return null;
        return homeSetWins > awaySetWins ? match.HomeTeamId : match.AwayTeamId;
    }
}

public sealed record ForfeitMatchCommand(Guid Id, Guid WinningTeamId) : IRequest<MatchDto?>;

public sealed class ForfeitMatchValidator : AbstractValidator<ForfeitMatchCommand>
{
    public ForfeitMatchValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.WinningTeamId).NotEmpty();
    }
}

public sealed class ForfeitMatchHandler : IRequestHandler<ForfeitMatchCommand, MatchDto?>
{
    private readonly IAppDbContext _db;
    private readonly IBracketProgressionService _progression;

    public ForfeitMatchHandler(IAppDbContext db, IBracketProgressionService progression)
    {
        _db = db;
        _progression = progression;
    }

    public async Task<MatchDto?> Handle(ForfeitMatchCommand request, CancellationToken ct)
    {
        var m = await _db.Matches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (m is null) return null;

        m.ForfeitTo(request.WinningTeamId);
        await _db.SaveChangesAsync(ct);

        if (m.BracketNodeId is not null)
            await _progression.AdvanceAsync(m.Id, ct);

        return m.ToDto();
    }
}

public sealed record StartMatchCommand(Guid Id) : IRequest<MatchDto?>;

public sealed class StartMatchHandler : IRequestHandler<StartMatchCommand, MatchDto?>
{
    private readonly IAppDbContext _db;
    public StartMatchHandler(IAppDbContext db) => _db = db;

    public async Task<MatchDto?> Handle(StartMatchCommand request, CancellationToken ct)
    {
        var m = await _db.Matches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (m is null) return null;
        m.Start();
        await _db.SaveChangesAsync(ct);
        return m.ToDto();
    }
}

public sealed record ChangeMatchRefereeCommand(Guid Id, Guid? RefTeamId) : IRequest<MatchDto?>;

public sealed class ChangeMatchRefereeHandler : IRequestHandler<ChangeMatchRefereeCommand, MatchDto?>
{
    private readonly IAppDbContext _db;
    public ChangeMatchRefereeHandler(IAppDbContext db) => _db = db;

    public async Task<MatchDto?> Handle(ChangeMatchRefereeCommand request, CancellationToken ct)
    {
        var m = await _db.Matches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (m is null) return null;
        m.AssignReferee(request.RefTeamId);
        await _db.SaveChangesAsync(ct);
        return m.ToDto();
    }
}

using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record UpsertTournamentSessionSettingsCommand(
    Guid TournamentId,
    TournamentSessionMode SessionMode,
    string? DefaultStartTime,
    string? AmStartTime,
    string? PmStartTime) : IRequest<TournamentDto?>;

public sealed class UpsertTournamentSessionSettingsValidator : AbstractValidator<UpsertTournamentSessionSettingsCommand>
{
    public UpsertTournamentSessionSettingsValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();

        When(x => x.SessionMode == TournamentSessionMode.SingleSession, () =>
        {
            RuleFor(x => x.DefaultStartTime)
                .NotEmpty()
                .WithMessage("Start time is required for single-session tournaments.")
                .Must(SessionTimeValidation.IsValidLocalTime)
                .WithMessage("Start time must be in HH:mm format.");
        });

        When(x => x.SessionMode == TournamentSessionMode.MultiWave, () =>
        {
            RuleFor(x => x.AmStartTime)
                .NotEmpty()
                .WithMessage("AM start time is required.")
                .Must(SessionTimeValidation.IsValidLocalTime)
                .WithMessage("AM start time must be in HH:mm format.");
            RuleFor(x => x.PmStartTime)
                .NotEmpty()
                .WithMessage("PM start time is required.")
                .Must(SessionTimeValidation.IsValidLocalTime)
                .WithMessage("PM start time must be in HH:mm format.");
        });
    }
}

public sealed class UpsertTournamentSessionSettingsHandler
    : IRequestHandler<UpsertTournamentSessionSettingsCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public UpsertTournamentSessionSettingsHandler(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TournamentDto?> Handle(UpsertTournamentSessionSettingsCommand request, CancellationToken ct)
    {
        _ = _tenant.RequireOrganizationId();

        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == request.TournamentId, ct);
        if (tournament is null)
            return null;

        tournament.SetSessionMode(request.SessionMode);

        if (request.SessionMode == TournamentSessionMode.SingleSession)
        {
            tournament.SetDefaultStartTime(request.DefaultStartTime);

            var divisions = await _db.Divisions
                .Where(d => d.TournamentId == request.TournamentId && d.WaveId != null)
                .ToListAsync(ct);
            foreach (var division in divisions)
                division.SetWave(null);
        }
        else
        {
            tournament.SetDefaultStartTime(null);
            await EnsureWaveAsync(tournament, "AM", 0, request.AmStartTime!, ct);
            await EnsureWaveAsync(tournament, "PM", 1, request.PmStartTime!, ct);
        }

        await _db.SaveChangesAsync(ct);
        return tournament.ToDto();
    }

    private async Task EnsureWaveAsync(
        Tournament tournament,
        string code,
        int displayOrder,
        string startTime,
        CancellationToken ct)
    {
        var wave = await _db.Waves.FirstOrDefaultAsync(
            w => w.TournamentId == tournament.Id && w.Code == code, ct);

        if (wave is null)
        {
            wave = new WaveEntity(tournament.OrganizationId, tournament.Id, code, displayOrder);
            _db.Waves.Add(wave);
        }

        wave.SetTimeWindow(startTime, null);
    }
}

internal static class SessionTimeValidation
{
    public static bool IsValidLocalTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length != 5 || trimmed[2] != ':')
            return false;

        return int.TryParse(trimmed.AsSpan(0, 2), out var hour)
               && int.TryParse(trimmed.AsSpan(3, 2), out var minute)
               && hour is >= 0 and <= 23
               && minute is >= 0 and <= 59;
    }
}

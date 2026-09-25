using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Waves;

public sealed record WaveDto(
    Guid Id,
    Guid TournamentId,
    string Code,
    int DisplayOrder,
    string? DefaultStartTime,
    string? DefaultEndTime,
    string? Notes);

internal static class WaveMapper
{
    public static WaveDto ToDto(this WaveEntity w) =>
        new(w.Id, w.TournamentId, w.Code, w.DisplayOrder, w.DefaultStartTime, w.DefaultEndTime, w.Notes);
}

public sealed record ListWavesQuery(Guid TournamentId) : IRequest<IReadOnlyList<WaveDto>>;

public sealed class ListWavesHandler : IRequestHandler<ListWavesQuery, IReadOnlyList<WaveDto>>
{
    private readonly IAppDbContext _db;

    public ListWavesHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WaveDto>> Handle(ListWavesQuery request, CancellationToken ct)
    {
        var items = await _db.Waves.AsNoTracking()
            .Where(w => w.TournamentId == request.TournamentId)
            .OrderBy(w => w.DisplayOrder)
            .ThenBy(w => w.Code)
            .ToListAsync(ct);
        return items.Select(WaveMapper.ToDto).ToList();
    }
}

public sealed record CreateWaveCommand(
    Guid TournamentId,
    string Code,
    string? DefaultStartTime,
    string? DefaultEndTime,
    string? Notes) : IRequest<WaveDto>;

public sealed class CreateWaveValidator : AbstractValidator<CreateWaveCommand>
{
    private static readonly string[] AllowedCodes = ["AM", "PM", "1", "2"];

    public CreateWaveValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20)
            .Must(c => AllowedCodes.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Code must be one of: AM, PM, 1, 2.");
    }
}

public sealed class CreateWaveHandler : IRequestHandler<CreateWaveCommand, WaveDto>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public CreateWaveHandler(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<WaveDto> Handle(CreateWaveCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var code = request.Code.Trim();
        if (string.Equals(code, "am", StringComparison.OrdinalIgnoreCase)) code = "AM";
        else if (string.Equals(code, "pm", StringComparison.OrdinalIgnoreCase)) code = "PM";

        var exists = await _db.Waves.AnyAsync(
            w => w.TournamentId == request.TournamentId && w.Code == code, ct);
        if (exists)
            throw new InvalidOperationException($"Wave code '{code}' already exists for this tournament.");

        ValidateTimeWindow(request.DefaultStartTime, request.DefaultEndTime);

        var maxOrder = await _db.Waves
            .Where(w => w.TournamentId == request.TournamentId)
            .Select(w => (int?)w.DisplayOrder)
            .MaxAsync(ct) ?? -1;

        var wave = new WaveEntity(orgId, request.TournamentId, code, maxOrder + 1);
        wave.SetTimeWindow(request.DefaultStartTime, request.DefaultEndTime);
        if (!string.IsNullOrWhiteSpace(request.Notes))
            wave.SetNotes(request.Notes.Trim());

        _db.Waves.Add(wave);
        await _db.SaveChangesAsync(ct);
        return wave.ToDto();
    }

    internal static void ValidateTimeWindow(string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return;
        if (string.Compare(start, end, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Default end time must be after start time.");
    }
}

public sealed record UpdateWaveCommand(
    Guid Id,
    string? DefaultStartTime,
    string? DefaultEndTime,
    string? Notes) : IRequest<WaveDto?>;

public sealed class UpdateWaveValidator : AbstractValidator<UpdateWaveCommand>
{
    public UpdateWaveValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class UpdateWaveHandler : IRequestHandler<UpdateWaveCommand, WaveDto?>
{
    private readonly IAppDbContext _db;

    public UpdateWaveHandler(IAppDbContext db) => _db = db;

    public async Task<WaveDto?> Handle(UpdateWaveCommand request, CancellationToken ct)
    {
        var wave = await _db.Waves.FirstOrDefaultAsync(w => w.Id == request.Id, ct);
        if (wave is null) return null;

        CreateWaveHandler.ValidateTimeWindow(request.DefaultStartTime, request.DefaultEndTime);
        wave.SetTimeWindow(request.DefaultStartTime, request.DefaultEndTime);
        wave.SetNotes(request.Notes);
        await _db.SaveChangesAsync(ct);
        return wave.ToDto();
    }
}

public sealed record DeleteWaveCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteWaveHandler : IRequestHandler<DeleteWaveCommand, bool>
{
    private readonly IAppDbContext _db;

    public DeleteWaveHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(DeleteWaveCommand request, CancellationToken ct)
    {
        var wave = await _db.Waves.FirstOrDefaultAsync(w => w.Id == request.Id, ct);
        if (wave is null) return false;

        _db.Waves.Remove(wave);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

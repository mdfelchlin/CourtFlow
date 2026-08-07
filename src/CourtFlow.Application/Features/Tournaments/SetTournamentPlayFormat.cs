using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record SetTournamentPlayFormatCommand(Guid TournamentId, DivisionFormat Format) : IRequest<TournamentDto?>;

public sealed class SetTournamentPlayFormatValidator : AbstractValidator<SetTournamentPlayFormatCommand>
{
    public SetTournamentPlayFormatValidator()
    {
        RuleFor(x => x.TournamentId).NotEmpty();
        RuleFor(x => x.Format).IsInEnum();
    }
}

public sealed class SetTournamentPlayFormatHandler : IRequestHandler<SetTournamentPlayFormatCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;

    public SetTournamentPlayFormatHandler(IAppDbContext db) => _db = db;

    public async Task<TournamentDto?> Handle(SetTournamentPlayFormatCommand request, CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == request.TournamentId, ct);
        if (tournament is null) return null;

        var divisions = await _db.Divisions.Where(d => d.TournamentId == request.TournamentId).ToListAsync(ct);
        if (divisions.Count == 0)
            return tournament.ToDto(null);

        var divisionIds = divisions.Select(d => d.Id).ToList();
        var blocked = await _db.Stages.AsNoTracking()
            .AnyAsync(s => divisionIds.Contains(s.DivisionId)
                           && (s.Status == StageStatus.InProgress || s.Status == StageStatus.Completed), ct);
        if (blocked)
            throw new InvalidOperationException("Cannot change play format while a stage is in progress or completed.");

        foreach (var division in divisions)
        {
            division.SetFormat(request.Format);
            await DefaultDivisionStages.EnsureMissingStagesForFormatAsync(_db, division, ct);
        }

        await _db.SaveChangesAsync(ct);
        return tournament.ToDto(request.Format);
    }
}

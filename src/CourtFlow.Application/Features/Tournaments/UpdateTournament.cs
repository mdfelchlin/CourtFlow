using CourtFlow.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record UpdateTournamentCommand(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes,
    Guid? TournamentPartnerId) : IRequest<TournamentDto?>;

public sealed class UpdateTournamentValidator : AbstractValidator<UpdateTournamentCommand>
{
    public UpdateTournamentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TimeZoneId).NotEmpty();
        RuleFor(x => x.DefaultMatchDurationMinutes).GreaterThan(0);
        RuleFor(x => x).Must(x => x.EndDate >= x.StartDate).WithMessage("End date must be on or after start date");
    }
}

public sealed class UpdateTournamentHandler : IRequestHandler<UpdateTournamentCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;

    public UpdateTournamentHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TournamentDto?> Handle(UpdateTournamentCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;

        t.Rename(request.Name);
        t.SetDescription(request.Description);
        t.SetDates(request.StartDate, request.EndDate);
        t.SetTimeZone(request.TimeZoneId);
        t.SetDefaultMatchDuration(request.DefaultMatchDurationMinutes);
        t.SetPartner(request.TournamentPartnerId);

        await _db.SaveChangesAsync(ct);
        return t.ToDto();
    }
}

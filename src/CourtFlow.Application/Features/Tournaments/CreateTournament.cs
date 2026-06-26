using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record CreateTournamentCommand(
    string Name,
    string? Description,
    string? WebsiteUrl,
    string? ContactEmail,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    int DefaultMatchDurationMinutes) : IRequest<TournamentDto>;

public sealed class CreateTournamentValidator : AbstractValidator<CreateTournamentCommand>
{
    public CreateTournamentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultMatchDurationMinutes).GreaterThan(0);
        RuleFor(x => x).Must(x => x.EndDate >= x.StartDate).WithMessage("End date must be on or after start date");
        RuleFor(x => x.WebsiteUrl).MaximumLength(500)
            .Must(TournamentContactValidation.IsValidWebsiteUrl)
            .WithMessage("Website must be a valid URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.WebsiteUrl));
        RuleFor(x => x.ContactEmail).MaximumLength(254).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}

public sealed class CreateTournamentHandler : IRequestHandler<CreateTournamentCommand, TournamentDto>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public CreateTournamentHandler(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TournamentDto> Handle(CreateTournamentCommand request, CancellationToken ct)
    {
        var orgId = _tenant.RequireOrganizationId();
        var tournament = new Tournament(orgId, request.Name, request.StartDate, request.EndDate, request.TimeZoneId, request.DefaultMatchDurationMinutes);
        if (request.Description is not null) tournament.SetDescription(request.Description);
        tournament.SetContactInfo(request.WebsiteUrl, request.ContactEmail);

        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync(ct);
        return tournament.ToDto();
    }
}

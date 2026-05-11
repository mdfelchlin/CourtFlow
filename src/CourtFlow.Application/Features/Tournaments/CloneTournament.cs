using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record CloneTournamentCommand(Guid SourceTournamentId) : IRequest<TournamentDto?>;

public sealed class CloneTournamentValidator : AbstractValidator<CloneTournamentCommand>
{
    public CloneTournamentValidator() => RuleFor(x => x.SourceTournamentId).NotEmpty();
}

public sealed class CloneTournamentHandler : IRequestHandler<CloneTournamentCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public CloneTournamentHandler(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TournamentDto?> Handle(CloneTournamentCommand request, CancellationToken ct)
    {
        _tenant.RequireOrganizationId();

        var source = await _db.Tournaments
            .AsNoTracking()
            .Include(t => t.Venues).ThenInclude(v => v.Courts)
            .Include(t => t.Divisions).ThenInclude(d => d.Pools)
            .Include(t => t.Divisions).ThenInclude(d => d.Teams)
            .FirstOrDefaultAsync(t => t.Id == request.SourceTournamentId, ct);

        if (source is null)
            return null;

        var copyName = TrimCopyName($"Copy of {source.Name.Trim()}");

        var copy = new Tournament(source.OrganizationId, copyName, source.StartDate, source.EndDate, source.TimeZoneId,
            source.DefaultMatchDurationMinutes);
        if (source.Description is not null)
            copy.SetDescription(source.Description);

        _db.Tournaments.Add(copy);

        foreach (var oldVenue in source.Venues.OrderBy(v => v.Name))
        {
            var venue = new Venue(source.OrganizationId, copy.Id, oldVenue.Name, oldVenue.Address);
            _db.Venues.Add(venue);
            foreach (var oc in oldVenue.Courts.OrderBy(c => c.Name))
                _db.Courts.Add(new Court(source.OrganizationId, venue.Id, oc.Name, oc.Tags.ToArray()));
        }

        var poolOldToNew = new Dictionary<Guid, Guid>();

        foreach (var oldDiv in source.Divisions.OrderBy(d => d.Name))
        {
            var div = new Division(source.OrganizationId, copy.Id, oldDiv.Name, oldDiv.Format, oldDiv.MaxTeams);
            _db.Divisions.Add(div);

            foreach (var op in oldDiv.Pools.OrderBy(p => p.Name))
            {
                var np = new Pool(source.OrganizationId, div.Id, op.Name);
                _db.Pools.Add(np);
                poolOldToNew[op.Id] = np.Id;
            }

            foreach (var ot in oldDiv.Teams.OrderBy(t => t.Name))
            {
                Guid? pid = ot.PoolId is { } p && poolOldToNew.TryGetValue(p, out var npId) ? npId : null;
                _db.Teams.Add(new Team(source.OrganizationId, div.Id, ot.Name, ot.Wave, pid, ot.Club, ot.SeedHint));
            }
        }

        _db.ScheduleVersions.Add(new ScheduleVersion(source.OrganizationId, copy.Id));

        await _db.SaveChangesAsync(ct);

        return copy.ToDto();
    }

    private static string TrimCopyName(string name) =>
        name.Length <= 200 ? name : name[..200];
}

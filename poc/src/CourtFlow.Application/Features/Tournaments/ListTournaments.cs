using CourtFlow.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record ListTournamentsQuery() : IRequest<IReadOnlyList<TournamentDto>>;

public sealed class ListTournamentsHandler : IRequestHandler<ListTournamentsQuery, IReadOnlyList<TournamentDto>>
{
    private readonly IAppDbContext _db;

    public ListTournamentsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TournamentDto>> Handle(ListTournamentsQuery request, CancellationToken ct)
    {
        var items = await _db.Tournaments.OrderByDescending(t => t.StartDate).ToListAsync(ct);
        var playFormats = await TournamentQueryHelper.ResolvePlayFormatsAsync(
            _db, items.Select(t => t.Id).ToList(), ct);
        return items.Select(t => t.ToDto(playFormats.GetValueOrDefault(t.Id))).ToList();
    }
}

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
        return items.Select(TournamentMapping.ToDto).ToList();
    }
}

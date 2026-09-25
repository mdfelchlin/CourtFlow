using CourtFlow.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record GetTournamentQuery(Guid Id) : IRequest<TournamentDto?>;

public sealed class GetTournamentHandler : IRequestHandler<GetTournamentQuery, TournamentDto?>
{
    private readonly IAppDbContext _db;

    public GetTournamentHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TournamentDto?> Handle(GetTournamentQuery request, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;
        var playFormat = await TournamentQueryHelper.ResolvePlayFormatAsync(_db, t.Id, ct);
        return t.ToDto(playFormat);
    }
}

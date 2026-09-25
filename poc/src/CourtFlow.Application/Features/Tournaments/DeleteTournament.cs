using CourtFlow.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record DeleteTournamentCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteTournamentHandler : IRequestHandler<DeleteTournamentCommand, bool>
{
    private readonly IAppDbContext _db;

    public DeleteTournamentHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(DeleteTournamentCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return false;
        _db.Tournaments.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

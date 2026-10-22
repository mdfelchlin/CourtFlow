using CourtFlow.Application;
using CourtFlow.Domain;

using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure;

public sealed class ClubRepository(CourtFlowDbContext db) : IClubRepository
{
    public async Task<IReadOnlyList<Club>> SearchAsync(string? search, CancellationToken cancellationToken)
    {
        var clubs = db.Clubs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = Club.Normalize(search);
            clubs = clubs.Where(club => club.NormalizedName.Contains(key));
        }

        return await clubs.OrderBy(club => club.NormalizedName).ToListAsync(cancellationToken);
    }

    public Task<Club?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Clubs.Include(club => club.Teams).FirstOrDefaultAsync(club => club.Id == id, cancellationToken);

    public Task<Club?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var key = Club.Normalize(name);
        return db.Clubs.Include(club => club.Teams).FirstOrDefaultAsync(club => club.NormalizedName == key, cancellationToken);
    }

    public async Task<IReadOnlyList<Club>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var keys = names.Select(Club.Normalize).Distinct().ToList();
        return await db.Clubs
            .Include(club => club.Teams)
            .Where(club => keys.Contains(club.NormalizedName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Club>> GetByTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken)
    {
        var ids = teamIds.Distinct().ToList();
        return await db.Clubs
            .Include(club => club.Teams)
            .Where(club => club.Teams.Any(team => ids.Contains(team.Id)))
            .ToListAsync(cancellationToken);
    }

    public void Add(Club club) => db.Clubs.Add(club);
}
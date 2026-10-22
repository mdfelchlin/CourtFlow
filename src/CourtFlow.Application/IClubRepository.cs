using CourtFlow.Domain;

namespace CourtFlow.Application;

public interface IClubRepository
{
    /// <summary>Clubs whose name contains the search text, ignoring case, ordered by name. Teams are not loaded.</summary>
    Task<IReadOnlyList<Club>> SearchAsync(string? search, CancellationToken cancellationToken);

    Task<Club?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Club?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<Club>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken);
    Task<IReadOnlyList<Club>> GetByTeamsAsync(IEnumerable<Guid> teamIds, CancellationToken cancellationToken);
    void Add(Club club);
}

public static class SaveConflict
{
    public const string Message = "This club or team was just saved by someone else. Try again.";
}
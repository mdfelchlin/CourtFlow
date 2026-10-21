namespace CourtFlow.Domain;

/// <summary>One team's place in a division's saved ranking. A division with no places is not ranked.</summary>
public sealed class RankingPlace
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid DivisionId { get; private set; }
    public Guid EntryId { get; private set; }
    public int Rank { get; private set; }
    public bool IsNew { get; private set; }

    private RankingPlace()
    {
    }

    internal RankingPlace(Guid organizationId, Guid tournamentId, Guid divisionId, Guid entryId, int rank, bool isNew)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        DivisionId = divisionId;
        EntryId = entryId;
        Rank = rank;
        IsNew = isNew;
    }

    internal void SetRank(int rank) => Rank = rank;

    internal void Accept() => IsNew = false;

    internal void MoveTo(Guid divisionId, int rank, bool isNew)
    {
        DivisionId = divisionId;
        Rank = rank;
        IsNew = isNew;
    }
}
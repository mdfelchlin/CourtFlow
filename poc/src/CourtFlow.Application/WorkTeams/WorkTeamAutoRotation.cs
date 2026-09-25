namespace CourtFlow.Application.WorkTeams;

/// <summary>next_in_pool work-team assignment per data-model §8.</summary>
public static class WorkTeamAutoRotation
{
    public static Guid? PickWorkTeamTeamId(
        IReadOnlyList<Guid> poolTeamIdsInSeedOrder,
        Guid homeTeamId,
        Guid awayTeamId,
        int matchIndexInPool)
    {
        var sitting = poolTeamIdsInSeedOrder
            .Where(id => id != homeTeamId && id != awayTeamId)
            .ToList();
        if (sitting.Count == 0)
            return null;

        return sitting[matchIndexInPool % sitting.Count];
    }
}

using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Common;
using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Persistence;

/// <summary>
/// Bulk deletes that avoid <see cref="DbUpdateConcurrencyException"/> on relational providers
/// and fall back to load-and-remove for the InMemory test provider.
/// </summary>
public static class EntityGraphDeletes
{
    public static Task<int> ClearPoolCourtAllocationsAsync(IAppDbContext db, Guid poolId, CancellationToken ct) =>
        BulkDeleteAsync(db, db.PoolCourtAllocations.Where(a => a.PoolId == poolId), ct);

    public static Task<int> ClearPoolEntriesAsync(IAppDbContext db, Guid poolId, CancellationToken ct) =>
        BulkDeleteAsync(db, db.PoolEntries.Where(e => e.PoolId == poolId), ct);

    public static async Task ClearTeamPoolAssignmentsAsync(IAppDbContext db, Guid poolId, CancellationToken ct)
    {
        var teams = await db.Teams.Where(t => t.PoolId == poolId).ToListAsync(ct);
        foreach (var team in teams)
            team.AssignPool(null);
    }

    public static async Task<int> DeletePoolIfOrphanedAsync(IAppDbContext db, Guid poolId, CancellationToken ct)
    {
        await ClearPoolCourtAllocationsAsync(db, poolId, ct);
        await ClearPoolEntriesAsync(db, poolId, ct);
        await ClearTeamPoolAssignmentsAsync(db, poolId, ct);
        return await BulkDeleteAsync(db, db.Pools.Where(p => p.Id == poolId), ct);
    }

    public static Task<int> ClearAdvancementRulesForStageAsync(IAppDbContext db, Guid sourceStageId, CancellationToken ct) =>
        BulkDeleteAsync(db, db.AdvancementRules.Where(r => r.SourceStageId == sourceStageId), ct);

    public static Task<int> ClearDivisionRankingsExceptAsync(
        IAppDbContext db,
        Guid divisionId,
        IReadOnlyCollection<Guid> keepTournamentTeamIds,
        CancellationToken ct)
    {
        var keep = keepTournamentTeamIds.ToHashSet();
        return BulkDeleteAsync(
            db,
            db.DivisionRankings.Where(r => r.DivisionId == divisionId && !keep.Contains(r.TournamentTeamId)),
            ct);
    }

    public static Task ClearDivisionPoolPlayMatchesAsync(
        IAppDbContext db,
        Guid divisionId,
        Guid poolStageId,
        Guid scheduleVersionId,
        CancellationToken ct) =>
        ClearPoolPlayMatchesAsync(
            db,
            divisionId,
            poolStageId,
            scheduleVersionId,
            poolIds: null,
            ct);

    public static Task ClearPoolPlayMatchesForPoolsAsync(
        IAppDbContext db,
        Guid divisionId,
        Guid poolStageId,
        Guid scheduleVersionId,
        IReadOnlyCollection<Guid> poolIds,
        CancellationToken ct) =>
        ClearPoolPlayMatchesAsync(
            db,
            divisionId,
            poolStageId,
            scheduleVersionId,
            poolIds,
            ct);

    private static async Task ClearPoolPlayMatchesAsync(
        IAppDbContext db,
        Guid divisionId,
        Guid poolStageId,
        Guid scheduleVersionId,
        IReadOnlyCollection<Guid>? poolIds,
        CancellationToken ct)
    {
        var query = db.Matches
            .Where(m => m.DivisionId == divisionId && m.PoolId != null && m.StageId == poolStageId);
        if (poolIds is { Count: > 0 })
            query = query.Where(m => m.PoolId != null && poolIds.Contains(m.PoolId.Value));

        var matchIds = await query.Select(m => m.Id).ToListAsync(ct);
        if (matchIds.Count == 0)
            return;

        await BulkDeleteAsync(
            db,
            db.MatchAssignments.Where(a => a.ScheduleVersionId == scheduleVersionId && matchIds.Contains(a.MatchId)),
            ct);

        await BulkDeleteAsync(db, db.MatchOfficials.Where(o => matchIds.Contains(o.MatchId)), ct);
        await BulkDeleteAsync(db, db.MatchSlotSources.Where(s => matchIds.Contains(s.MatchId)), ct);
        await BulkDeleteAsync(db, db.Matches.Where(m => matchIds.Contains(m.Id)), ct);
    }

    public static Task<int> ClearMatchSlotSourcesAsync(IAppDbContext db, Guid matchId, CancellationToken ct) =>
        BulkDeleteAsync(db, db.MatchSlotSources.Where(s => s.MatchId == matchId), ct);

    public static Task<int> ClearBracketSeedSlotsAsync(IAppDbContext db, Guid bracketId, CancellationToken ct) =>
        BulkDeleteAsync(db, db.BracketSeedSlots.Where(s => s.BracketId == bracketId), ct);

    public static Task<int> DeleteBracketAsync(IAppDbContext db, Guid bracketId, CancellationToken ct) =>
        BulkDeleteQueryAsync(db, db.Brackets.Where(b => b.Id == bracketId), ct);

    public static Task<int> BulkDeleteQueryAsync<TEntity>(
        IAppDbContext db,
        IQueryable<TEntity> query,
        CancellationToken ct) where TEntity : Entity =>
        BulkDeleteAsync(db, query, ct);

    private static async Task<int> BulkDeleteAsync<TEntity>(
        IAppDbContext db,
        IQueryable<TEntity> query,
        CancellationToken ct) where TEntity : Entity
    {
        if (db is not DbContext context)
            throw new InvalidOperationException("IAppDbContext must be a DbContext.");

        var providerName = context.Database.ProviderName ?? string.Empty;
        var supportsExecuteDelete = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
            || providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

        if (supportsExecuteDelete)
        {
            var deletedIds = await query.Select(e => e.Id).ToListAsync(ct);
            if (deletedIds.Count == 0)
                return 0;

            var deleted = await query.ExecuteDeleteAsync(ct);
            DetachTracked<TEntity>(context, deletedIds);
            return deleted;
        }

        var entities = await query.ToListAsync(ct);
        if (entities.Count == 0)
            return 0;

        context.RemoveRange(entities);
        return entities.Count;
    }

    /// <summary>
    /// Rows removed via <see cref="EntityFrameworkQueryableExtensions.ExecuteDeleteAsync"/> are not
    /// removed from the change tracker; detach them so a later SaveChanges does not re-delete/update.
    /// </summary>
    private static void DetachTracked<TEntity>(DbContext context, IReadOnlyCollection<Guid> deletedIds)
        where TEntity : Entity
    {
        var idSet = deletedIds as HashSet<Guid> ?? deletedIds.ToHashSet();
        foreach (var entry in context.ChangeTracker.Entries<TEntity>())
        {
            if (idSet.Contains(entry.Entity.Id))
                entry.State = EntityState.Detached;
        }
    }
}

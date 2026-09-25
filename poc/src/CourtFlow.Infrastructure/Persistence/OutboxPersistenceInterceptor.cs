using CourtFlow.Domain.Common;
using CourtFlow.Domain.Entities;
using CourtFlow.Infrastructure.Persistence.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CourtFlow.Infrastructure.Persistence;

/// <summary>
/// Copies accumulated <see cref="Entity.DomainEvents"/> into outbox rows in the same transaction,
/// clearing events only after a successful <see cref="DbContext.SaveChangesAsync"/> (see EF saved-changes interception).
/// </summary>
public sealed class OutboxPersistenceInterceptor(OutboxEventSerializer serializer) : SaveChangesInterceptor
{
    private readonly List<Entity> _pendingEventClear = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _pendingEventClear.Clear();
        if (eventData.Context is AppDbContext ctx)
            AppendOutboxEntries(ctx);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result > 0 && _pendingEventClear.Count > 0)
        {
            foreach (var e in _pendingEventClear.Distinct())
                e.ClearDomainEvents();
            _pendingEventClear.Clear();
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        _pendingEventClear.Clear();
        if (eventData.Context is AppDbContext ctx)
            AppendOutboxEntries(ctx);

        return base.SavingChanges(eventData, result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (result > 0 && _pendingEventClear.Count > 0)
        {
            foreach (var e in _pendingEventClear.Distinct())
                e.ClearDomainEvents();
            _pendingEventClear.Clear();
        }

        return base.SavedChanges(eventData, result);
    }

    private void AppendOutboxEntries(AppDbContext ctx)
    {
        foreach (var entry in ctx.ChangeTracker.Entries<Entity>())
        {
            if (entry.Entity.DomainEvents.Count == 0)
                continue;

            foreach (var evt in entry.Entity.DomainEvents)
            {
                var (typeName, json) = serializer.Serialize(evt);
                ctx.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    TypeName = typeName,
                    PayloadJson = json,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            _pendingEventClear.Add(entry.Entity);
        }
    }
}

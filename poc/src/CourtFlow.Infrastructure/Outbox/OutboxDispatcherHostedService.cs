using System.Text.Json;
using CourtFlow.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.Outbox;

public sealed class OutboxDispatcherHostedService(IServiceScopeFactory scopes, ILogger<OutboxDispatcherHostedService> log)
    : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Outbox dispatcher iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task DrainBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var pending = await db.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.ProcessedAtUtc == null && m.AttemptCount < 20)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        foreach (var row in pending)
        {
            try
            {
                var evtType = Type.GetType(row.TypeName, throwOnError: false)
                    ?? throw new InvalidOperationException($"Unknown CLR type '{row.TypeName}'.");
                var payload = JsonSerializer.Deserialize(row.PayloadJson, evtType, Json)
                    ?? throw new InvalidOperationException($"Empty payload for {row.Id}");
                if (payload is INotification notification)
                    await mediator.Publish(notification, ct);

                row.ProcessedAtUtc = DateTime.UtcNow;
                row.LastError = null;
            }
            catch (Exception ex)
            {
                row.AttemptCount++;
                row.LastError = ex.Message;
                log.LogWarning(ex, "Failed to process outbox message {MessageId}", row.Id);
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}

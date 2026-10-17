using CourtFlow.Infrastructure.Email;

namespace CourtFlow.Api;

/// <summary>Sends saved emails. A failed send waits and tries again, so an email provider outage never loses a message.</summary>
public sealed class EmailOutboxWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<EmailOutboxSender>();
            await sender.SendDueAsync(clock.GetUtcNow(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The email outbox run failed. It will run again later.");
        }
    }
}

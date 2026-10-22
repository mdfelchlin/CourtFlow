using CourtFlow.Application.Access;

namespace CourtFlow.Api;

/// <summary>Reminds operators a day before a request expires, and expires requests after seven days.</summary>
public sealed partial class AccessRequestSweepWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AccessRequestSweepWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

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
            var sweep = scope.ServiceProvider.GetRequiredService<AccessRequestSweep>();
            var result = await sweep.RunAsync(clock.GetUtcNow(), cancellationToken);
            if ((result.Reminded > 0 || result.Expired > 0) && logger.IsEnabled(LogLevel.Information))
                LogReminders(logger, result.Reminded, result.Expired);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogSweepFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Access requests: {Reminded} reminder(s) sent, {Expired} expired.")]
    private static partial void LogReminders(ILogger logger, int reminded, int expired);

    [LoggerMessage(Level = LogLevel.Error, Message = "The access request sweep failed. It will run again later.")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
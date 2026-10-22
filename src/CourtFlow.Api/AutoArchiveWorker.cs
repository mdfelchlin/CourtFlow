using CourtFlow.Application.Tournaments;

namespace CourtFlow.Api;

/// <summary>
/// Archives published tournaments whose 270 days are up. It runs at startup and then hourly, which keeps
/// the "within one day" promise while the API is running, and catches up on overdue ones after an outage.
/// </summary>
public sealed partial class AutoArchiveWorker(IServiceScopeFactory scopes, ILogger<AutoArchiveWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

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
            var useCases = scope.ServiceProvider.GetRequiredService<TournamentUseCases>();
            var archived = await useCases.ArchiveExpiredAsync(cancellationToken);
            if (archived > 0 && logger.IsEnabled(LogLevel.Information))
                LogArchived(logger, archived);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogArchiveFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Archived {Count} tournament(s) 270 days after publishing.")]
    private static partial void LogArchived(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "The automatic tournament archive failed. It will run again later.")]
    private static partial void LogArchiveFailed(ILogger logger, Exception exception);
}
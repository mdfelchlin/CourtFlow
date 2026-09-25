using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.HostedServices;

/// <summary>Archives published tournaments N days after end_date (PUB-07).</summary>
public sealed class TournamentAutoArchiveHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);
    private const int ArchiveAfterDays = 14;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TournamentAutoArchiveHostedService> _logger;

    public TournamentAutoArchiveHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<TournamentAutoArchiveHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ArchiveEligibleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-archive sweep failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ArchiveEligibleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-ArchiveAfterDays));

        var candidates = await db.Tournaments
            .Where(t => t.PublicationStatus == TournamentPublicationStatus.Published && t.EndDate <= cutoff)
            .ToListAsync(ct);

        foreach (var t in candidates)
        {
            t.Archive();
            _logger.LogInformation("Auto-archived tournament {TournamentId} ({Name})", t.Id, t.Name);
        }

        if (candidates.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}

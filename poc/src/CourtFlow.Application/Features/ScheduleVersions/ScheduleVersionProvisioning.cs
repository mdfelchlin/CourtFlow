using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.ScheduleVersions;

public static class ScheduleVersionProvisioning
{
    /// <summary>Gets or creates the tournament's initial draft schedule version.</summary>
    public static async Task<ScheduleVersion> EnsureCurrentAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct = default)
    {
        var working = await GetLatestNonSupersededAsync(db, tournamentId, ct);
        if (working is not null)
            return working;

        return await CreateDraftAsync(db, tournamentId, ct);
    }

    /// <summary>Legacy alias used by preview/apply move handlers.</summary>
    public static Task<ScheduleVersion> EnsurePublishedAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct = default)
        => EnsureCurrentAsync(db, tournamentId, ct);

    /// <summary>Returns the published schedule version, if any.</summary>
    public static async Task<ScheduleVersion?> GetPublishedAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct = default)
        => await db.ScheduleVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.TournamentId == tournamentId && v.Status == ScheduleVersionStatus.Published, ct);

    /// <summary>Latest non-superseded version (draft, preview, or published).</summary>
    public static async Task<ScheduleVersion?> GetLatestNonSupersededAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct = default)
        => await db.ScheduleVersions
            .Where(v => v.TournamentId == tournamentId && v.Status != ScheduleVersionStatus.Superseded)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    /// <summary>Ensures an editable version exists; forks when the latest version is published.</summary>
    public static async Task<ScheduleVersion> EnsureWorkingForMutationAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct = default)
    {
        var latest = await GetLatestNonSupersededAsync(db, tournamentId, ct);
        if (latest is null)
            return await CreateDraftAsync(db, tournamentId, ct);

        if (latest.Status == ScheduleVersionStatus.Published)
            return await ForkAsync(db, latest, ct);

        return latest;
    }

    public static async Task<ScheduleVersion> ForkAsync(IAppDbContext db, ScheduleVersion source, CancellationToken ct = default)
    {
        if (source.Status != ScheduleVersionStatus.Published)
            throw new InvalidOperationException("Only published schedule versions can be forked.");

        var fork = new ScheduleVersion(source.OrganizationId, source.TournamentId);
        db.ScheduleVersions.Add(fork);
        source.Supersede(fork.Id);

        var assignments = await db.MatchAssignments
            .Where(a => a.ScheduleVersionId == source.Id)
            .ToListAsync(ct);

        foreach (var a in assignments)
        {
            db.MatchAssignments.Add(new MatchAssignment(
                a.OrganizationId,
                fork.Id,
                a.MatchId,
                a.CourtId,
                a.ScheduledStart,
                a.ScheduledEnd));
        }

        await db.SaveChangesAsync(ct);
        return fork;
    }

    public static async Task<ScheduleVersion> PublishAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct = default)
    {
        var version = await GetLatestNonSupersededAsync(db, tournamentId, ct)
                      ?? throw new InvalidOperationException("No schedule version exists to publish.");

        if (version.Status == ScheduleVersionStatus.Published)
            return version;

        if (version.Status == ScheduleVersionStatus.Superseded)
            throw new InvalidOperationException("Cannot publish a superseded schedule version.");

        var existingPublished = await db.ScheduleVersions
            .Where(v => v.TournamentId == tournamentId && v.Status == ScheduleVersionStatus.Published && v.Id != version.Id)
            .ToListAsync(ct);

        foreach (var old in existingPublished)
            old.Supersede(version.Id);

        if (version.Status == ScheduleVersionStatus.Draft)
            version.TransitionTo(ScheduleVersionStatus.Preview);

        version.TransitionTo(ScheduleVersionStatus.Published);
        await db.SaveChangesAsync(ct);
        return version;
    }

    private static async Task<ScheduleVersion> CreateDraftAsync(IAppDbContext db, Guid tournamentId, CancellationToken ct)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId, ct)
                         ?? throw new InvalidOperationException("Tournament not found.");

        var created = new ScheduleVersion(tournament.OrganizationId, tournamentId);
        db.ScheduleVersions.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}

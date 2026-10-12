using CourtFlow.Application;
using CourtFlow.Application.Teams;
using CourtFlow.Application.Teams.Import;
using Microsoft.AspNetCore.Mvc;


namespace CourtFlow.Api;

public static class TeamEndpoints
{
    // Above the reader's 2 MB limit, so an oversized file gets the spec's message instead of a bare 413.
    private const long ImportRequestLimit = 10 * 1024 * 1024;

    public static void MapTeamEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tournaments/{tournamentId:guid}/teams", async (
            Guid tournamentId,
            TeamUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.ListAsync(tournamentId, cancellationToken) is { } teams
                ? Results.Ok(teams)
                : Results.NotFound());

        app.MapPost("/api/tournaments/{tournamentId:guid}/teams", async (
            Guid tournamentId,
            AddTournamentTeam body,
            TeamUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.AddAsync(tournamentId, body, cancellationToken) is { } team
                    ? Results.Created($"/api/tournament-teams/{team.Id}", team)
                    : Results.NotFound()));

        app.MapPut("/api/tournament-teams/{entryId:guid}", async (
            Guid entryId,
            UpdateTournamentTeam body,
            TeamUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.UpdateAsync(entryId, body, cancellationToken) is { } team
                    ? Results.Ok(team)
                    : Results.NotFound()));

        app.MapDelete("/api/tournament-teams/{entryId:guid}", async (
            Guid entryId,
            TeamUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.DeleteAsync(entryId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());

        app.MapGet("/api/clubs", async (
            string? search,
            TeamUseCases useCases,
            CancellationToken cancellationToken) =>
            Results.Ok(await useCases.SearchClubsAsync(search, cancellationToken)));

        app.MapGet("/api/clubs/{clubId:guid}/teams", async (
            Guid clubId,
            Guid? tournamentId,
            TeamUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.ListClubTeamsAsync(clubId, tournamentId, cancellationToken) is { } teams
                ? Results.Ok(teams)
                : Results.NotFound());

        app.MapPost("/api/tournaments/{tournamentId:guid}/team-imports/preview", async (
            Guid tournamentId,
            IFormFile file,
            TeamImportUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.PreviewAsync(tournamentId, await ReadAsync(file, cancellationToken), cancellationToken) is { } preview
                ? Results.Ok(preview)
                : Results.NotFound())
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ImportRequestLimit));

        app.MapPost("/api/tournaments/{tournamentId:guid}/team-imports", async (
            Guid tournamentId,
            IFormFile file,
            TeamImportUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.ImportAsync(tournamentId, await ReadAsync(file, cancellationToken), cancellationToken) switch
                {
                    null => Results.NotFound(),
                    { Imported: { } imported } => Results.Ok(imported),
                    { Stale: var stale } => Results.Conflict(stale),
                }))
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(ImportRequestLimit));
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}

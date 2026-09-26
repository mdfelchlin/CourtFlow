using CourtFlow.Application;
using CourtFlow.Application.Venues;

namespace CourtFlow.Api;

public static class VenueEndpoints
{
    public static void MapVenueEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tournaments/{tournamentId:guid}/venues", async (
            Guid tournamentId,
            VenueUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.ListAsync(tournamentId, cancellationToken) is { } venues
                ? Results.Ok(venues)
                : Results.NotFound());

        app.MapPost("/api/tournaments/{tournamentId:guid}/venues", async (
            Guid tournamentId,
            SaveVenue body,
            VenueUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.CreateAsync(tournamentId, body, cancellationToken) is { } venue
                    ? Results.Created($"/api/venues/{venue.Id}", venue)
                    : Results.NotFound()));

        app.MapPut("/api/venues/{venueId:guid}", async (
            Guid venueId,
            SaveVenue body,
            VenueUseCases useCases,
            CancellationToken cancellationToken) =>
            await TournamentEndpoints.RejectRules(async () =>
                await useCases.UpdateAsync(venueId, body, cancellationToken) is { } venue
                    ? Results.Ok(venue)
                    : Results.NotFound()));

        app.MapDelete("/api/venues/{venueId:guid}", async (
            Guid venueId,
            VenueUseCases useCases,
            CancellationToken cancellationToken) =>
            await useCases.DeleteAsync(venueId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound());
    }
}

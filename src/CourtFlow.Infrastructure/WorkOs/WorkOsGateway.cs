using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using CourtFlow.Application.Access;

using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.WorkOs;

public sealed class WorkOsOptions
{
    public string ApiKey { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.workos.com/";
}

/// <summary>
/// WorkOS AuthKit over its REST API, with the secret key as a bearer token. Sign-in uses the hosted AuthKit page;
/// invitations and the Waitlist are plain calls. Errors WorkOS reports as expected states, such as an entry already
/// denied or an invitation already revoked, are treated as done.
/// </summary>
public sealed partial class WorkOsGateway(HttpClient http, WorkOsOptions options, WebOptions web, ILogger<WorkOsGateway> logger) : IWorkOs
{
    private const string UserAlreadyExists = "user_already_exists";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Without the screen hint, AuthKit opens on the waitlist form while the waitlist is enabled.
    public string GetSignInUrl(string state) =>
        $"{BaseUrl}user_management/authorize?response_type=code&provider=authkit&screen_hint=sign-in" +
        $"&client_id={Uri.EscapeDataString(options.ClientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(options.RedirectUri)}" +
        $"&state={Uri.EscapeDataString(state)}";

    public string GetLogoutUrl(string sessionId) =>
        $"{BaseUrl}user_management/sessions/logout?session_id={Uri.EscapeDataString(sessionId)}" +
        $"&return_to={Uri.EscapeDataString(web.Link("/"))}";

    public async Task<WorkOsSignIn> AuthenticateWithCodeAsync(string code, CancellationToken cancellationToken)
    {
        var body = await AuthenticateAsync(new { client_id = options.ClientId, client_secret = options.ApiKey, grant_type = "authorization_code", code }, cancellationToken)
            ?? throw new WorkOsUnavailableException("WorkOS refused the sign-in code.");

        return new WorkOsSignIn(
            body.User.Id,
            body.User.Email,
            body.User.FirstName,
            body.User.LastName,
            SessionIdFrom(body.AccessToken),
            body.RefreshToken);
    }

    public async Task<string> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var body = await AuthenticateAsync(new { client_id = options.ClientId, client_secret = options.ApiKey, grant_type = "refresh_token", refresh_token = refreshToken }, cancellationToken)
            ?? throw new WorkOsSessionEndedException("WorkOS refused to refresh the session.");
        return body.RefreshToken;
    }

    public async Task<string> CreateWaitlistEntryAsync(string email, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "user_management/waitlists/default/entries",
            new { email, additional_fields = fields, send_confirmation_email = false }, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (await ReadAsync<IdOnly>(response, cancellationToken)).Id;
        if (await ErrorCodeAsync(response, cancellationToken) == UserAlreadyExists)
            return "";

        throw await UnavailableAsync(response, "create a Waitlist entry", cancellationToken);
    }

    /// <summary>Approving creates WorkOS's invitation; its ID is found by listing the email's newest pending invitation.</summary>
    public async Task<WorkOsInvitation> ApproveWaitlistEntryAsync(string entryId, string email, CancellationToken cancellationToken)
    {
        using (var response = await SendAsync(HttpMethod.Post, $"user_management/waitlist_entries/{Uri.EscapeDataString(entryId)}/approve", null, cancellationToken))
        {
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.UnprocessableEntity)
                throw await UnavailableAsync(response, "approve a Waitlist entry", cancellationToken);
        }

        return await NewestPendingInvitationAsync(email, cancellationToken) ?? await SendInvitationAsync(email, cancellationToken);
    }

    public async Task DenyWaitlistEntryAsync(string entryId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"user_management/waitlist_entries/{Uri.EscapeDataString(entryId)}/deny", null, cancellationToken);
        await EnsureDoneAsync(response, "deny a Waitlist entry", cancellationToken);
    }

    public async Task DeleteWaitlistEntryAsync(string entryId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"user_management/waitlist_entries/{Uri.EscapeDataString(entryId)}", null, cancellationToken);
        await EnsureDoneAsync(response, "delete a Waitlist entry", cancellationToken);
    }

    public async Task<bool> WaitlistEntryExistsAsync(string entryId, string email, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"user_management/waitlists/default/entries?email={Uri.EscapeDataString(email)}&limit=100", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        if (!response.IsSuccessStatusCode)
            throw await UnavailableAsync(response, "list Waitlist entries", cancellationToken);

        var list = await ReadAsync<ListOf<IdOnly>>(response, cancellationToken);
        return list.Data.Any(entry => entry.Id == entryId);
    }

    public async Task<WorkOsInvitation> SendInvitationAsync(string email, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "user_management/invitations", new { email, expires_in_days = 7 }, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var invitation = await ReadAsync<InvitationBody>(response, cancellationToken);
            return new WorkOsInvitation(invitation.Id, invitation.AcceptInvitationUrl);
        }

        if (await ErrorCodeAsync(response, cancellationToken) == UserAlreadyExists)
            return WorkOsInvitation.NotNeeded;
        if ((int)response.StatusCode is >= 400 and < 500 && await NewestPendingInvitationAsync(email, cancellationToken) is { } pending)
            return pending;

        throw await UnavailableAsync(response, "send an invitation", cancellationToken);
    }

    public async Task RevokeInvitationAsync(string invitationId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"user_management/invitations/{Uri.EscapeDataString(invitationId)}/revoke", null, cancellationToken);
        await EnsureDoneAsync(response, "revoke an invitation", cancellationToken);
    }

    private async Task<WorkOsInvitation?> NewestPendingInvitationAsync(string email, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"user_management/invitations?email={Uri.EscapeDataString(email)}&order=desc&limit=10", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await UnavailableAsync(response, "list invitations", cancellationToken);

        var list = await ReadAsync<ListOf<InvitationBody>>(response, cancellationToken);
        return list.Data.FirstOrDefault(invitation => invitation.State == "pending") is { } pending
            ? new WorkOsInvitation(pending.Id, pending.AcceptInvitationUrl)
            : null;
    }

    /// <summary>Null when WorkOS refuses the grant; network failures and server errors throw.</summary>
    private async Task<AuthenticateBody?> AuthenticateAsync(object body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "user_management/authenticate", body, cancellationToken, authorize: false);
        if (response.IsSuccessStatusCode)
            return await ReadAsync<AuthenticateBody>(response, cancellationToken);
        if ((int)response.StatusCode is >= 400 and < 500)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            LogAuthenticationRefused(logger, (int)response.StatusCode, responseBody);
            return null;
        }

        throw await UnavailableAsync(response, "authenticate", cancellationToken);
    }

    /// <summary>A 4xx means the thing is already in the wanted state or gone; only server and network failures throw.</summary>
    private async Task EnsureDoneAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        if ((int)response.StatusCode is >= 400 and < 500)
        {
            if (logger.IsEnabled(LogLevel.Information))
                LogTreatedAsDone(logger, (int)response.StatusCode, action);
            return;
        }

        throw await UnavailableAsync(response, action, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken, bool authorize = true)
    {
        using var request = new HttpRequestMessage(method, BaseUrl + path);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        if (authorize)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new WorkOsUnavailableException("WorkOS could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkOsUnavailableException("WorkOS did not answer in time.", exception);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new WorkOsUnavailableException("WorkOS answered with an empty body.");

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<ErrorBody>(Json, cancellationToken))?.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<WorkOsUnavailableException> UnavailableAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        LogWorkOsFailed(logger, (int)response.StatusCode, action, body);
        return new WorkOsUnavailableException($"WorkOS could not {action}.");
    }

    /// <summary>The session ID is the access token's <c>sid</c> claim. The token came straight from WorkOS over TLS, so it is read, not verified.</summary>
    private static string? SessionIdFrom(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("sid", out var sid) ? sid.GetString() : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "WorkOS refused an authentication: {Status} {Body}")]
    private static partial void LogAuthenticationRefused(ILogger logger, int status, string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "WorkOS answered {Status} when asked to {Action}; treating it as done.")]
    private static partial void LogTreatedAsDone(ILogger logger, int status, string action);

    [LoggerMessage(Level = LogLevel.Error, Message = "WorkOS answered {Status} when asked to {Action}: {Body}")]
    private static partial void LogWorkOsFailed(ILogger logger, int status, string action, string body);

    private string BaseUrl => options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : options.ApiBaseUrl + "/";

    private sealed record IdOnly(string Id);

    private sealed record ListOf<T>(List<T> Data);

    private sealed record ErrorBody(string? Code, string? Message);

    private sealed record InvitationBody(string Id, string State, string? AcceptInvitationUrl);

    private sealed record UserBody(string Id, string Email, string? FirstName, string? LastName);

    private sealed record AuthenticateBody(UserBody User, string AccessToken, string RefreshToken);
}
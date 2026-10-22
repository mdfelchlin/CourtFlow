namespace CourtFlow.Application.Access;

public sealed record WorkOsSignIn(
    string UserId,
    string Email,
    string? FirstName,
    string? LastName,
    string? SessionId,
    string RefreshToken)
{
    public string Name => string.Join(' ', new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
}

/// <summary>
/// A WorkOS sign-up invitation. <see cref="Id"/> is empty and <see cref="AcceptUrl"/> null when the person already
/// has a WorkOS account: they need no invitation to get past the sign-up gate, only to sign in.
/// </summary>
public sealed record WorkOsInvitation(string Id, string? AcceptUrl)
{
    public static readonly WorkOsInvitation NotNeeded = new("", null);

    public bool Exists => Id.Length > 0;
}

/// <summary>WorkOS could not be reached, or refused a call CourtFlow expected to succeed.</summary>
public sealed class WorkOsUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>WorkOS refused to refresh the session, so it has ended there.</summary>
public sealed class WorkOsSessionEndedException(string message) : Exception(message);

/// <summary>Everything CourtFlow asks of WorkOS. Automated tests replace it with a fake and never call WorkOS.</summary>
public interface IWorkOs
{
    string GetSignInUrl(string state);
    Task<WorkOsSignIn> AuthenticateWithCodeAsync(string code, CancellationToken cancellationToken);
    Task<string> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    string GetLogoutUrl(string sessionId);

    /// <summary>Returns the entry ID, or an empty string when the email already has a WorkOS account.</summary>
    Task<string> CreateWaitlistEntryAsync(string email, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken);
    Task<WorkOsInvitation> ApproveWaitlistEntryAsync(string entryId, string email, CancellationToken cancellationToken);
    Task DenyWaitlistEntryAsync(string entryId, CancellationToken cancellationToken);
    Task DeleteWaitlistEntryAsync(string entryId, CancellationToken cancellationToken);
    Task<bool> WaitlistEntryExistsAsync(string entryId, string email, CancellationToken cancellationToken);

    Task<WorkOsInvitation> SendInvitationAsync(string email, CancellationToken cancellationToken);
    Task RevokeInvitationAsync(string invitationId, CancellationToken cancellationToken);
}
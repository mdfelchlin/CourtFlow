using CourtFlow.Application.Access;

namespace CourtFlow.Api.Tests;

/// <summary>Stands in for WorkOS: records each call, and can be told a person exists, an entry is gone, or WorkOS is down.</summary>
public sealed class FakeWorkOs : IWorkOs
{
    public const string AuthorizeUrl = "https://workos.test/authorize";
    public const string LogoutUrl = "https://workos.test/logout";

    public sealed record WaitlistEntry(string Id, string Email, IReadOnlyDictionary<string, string> Fields);

    public bool Unavailable { get; set; }
    public bool SessionEnded { get; set; }
    public Dictionary<string, WorkOsSignIn> Codes { get; } = [];
    public HashSet<string> ExistingUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> MissingEntries { get; } = [];
    public List<WaitlistEntry> Entries { get; } = [];
    public List<string> Approved { get; } = [];
    public List<string> Denied { get; } = [];
    public List<string> Deleted { get; } = [];
    public List<string> InvitationsSentTo { get; } = [];
    public List<string> Revoked { get; } = [];
    public int Refreshes { get; private set; }

    private int _next;

    public void Reset()
    {
        Unavailable = false;
        SessionEnded = false;
        Codes.Clear();
        ExistingUsers.Clear();
        MissingEntries.Clear();
        Entries.Clear();
        Approved.Clear();
        Denied.Clear();
        Deleted.Clear();
        InvitationsSentTo.Clear();
        Revoked.Clear();
        Refreshes = 0;
    }

    public string GetSignInUrl(string state) => $"{AuthorizeUrl}?state={Uri.EscapeDataString(state)}";

    public string GetLogoutUrl(string sessionId) => $"{LogoutUrl}?session_id={Uri.EscapeDataString(sessionId)}";

    public Task<WorkOsSignIn> AuthenticateWithCodeAsync(string code, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return Codes.TryGetValue(code, out var person)
            ? Task.FromResult(person)
            : throw new WorkOsUnavailableException("Unknown code.");
    }

    public Task<string> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (SessionEnded)
            throw new WorkOsSessionEndedException("Ended.");
        Refreshes++;
        return Task.FromResult($"{refreshToken}+");
    }

    public Task<string> CreateWaitlistEntryAsync(string email, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (ExistingUsers.Contains(email))
            return Task.FromResult("");
        var entry = new WaitlistEntry(NextId("wle"), email, fields);
        Entries.Add(entry);
        return Task.FromResult(entry.Id);
    }

    public Task<WorkOsInvitation> ApproveWaitlistEntryAsync(string entryId, string email, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        Approved.Add(entryId);
        return SendInvitationAsync(email, cancellationToken);
    }

    public Task DenyWaitlistEntryAsync(string entryId, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        Denied.Add(entryId);
        return Task.CompletedTask;
    }

    public Task DeleteWaitlistEntryAsync(string entryId, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        Deleted.Add(entryId);
        return Task.CompletedTask;
    }

    public Task<bool> WaitlistEntryExistsAsync(string entryId, string email, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        return Task.FromResult(!MissingEntries.Contains(entryId));
    }

    public Task<WorkOsInvitation> SendInvitationAsync(string email, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        if (ExistingUsers.Contains(email))
            return Task.FromResult(WorkOsInvitation.NotNeeded);
        InvitationsSentTo.Add(email);
        var id = NextId("inv");
        return Task.FromResult(new WorkOsInvitation(id, $"https://workos.test/invite/{id}"));
    }

    public Task RevokeInvitationAsync(string invitationId, CancellationToken cancellationToken)
    {
        ThrowIfUnavailable();
        Revoked.Add(invitationId);
        return Task.CompletedTask;
    }

    private string NextId(string prefix) => $"{prefix}_{Interlocked.Increment(ref _next)}";

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
            throw new WorkOsUnavailableException("WorkOS is down in this test.");
    }
}

public sealed class FakeCaptcha : ICaptchaVerifier
{
    public bool Passes { get; set; } = true;
    public List<string?> Tokens { get; } = [];

    public Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        Tokens.Add(token);
        return Task.FromResult(Passes);
    }
}

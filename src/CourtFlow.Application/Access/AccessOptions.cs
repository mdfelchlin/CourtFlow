using CourtFlow.Domain;

namespace CourtFlow.Application.Access;

/// <summary>Operators are named by email in configuration. Each has the same powers.</summary>
public sealed class OperatorOptions
{
    public string[] Emails { get; set; } = [];

    public bool IsOperator(string? email)
    {
        var normalized = EmailAddress.Normalize(email);
        return normalized.Length > 0 && Emails.Any(entry => EmailAddress.Normalize(entry) == normalized);
    }

    public IReadOnlyList<string> Normalized() =>
        Emails.Select(EmailAddress.Normalize).Where(email => email.Length > 0).Distinct().ToList();
}

/// <summary>The Blazor app's address, for links in emails and redirects after sign-in.</summary>
public sealed class WebOptions
{
    public string BaseUrl { get; set; } = "https://localhost:7211/";

    public string Link(string path) => BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
}

/// <summary>Local sign-in help. <see cref="Enabled"/> is true only in the Development environment.</summary>
public sealed class DevelopmentOptions
{
    public bool Enabled { get; set; }
    public string[] TestOrganizationOwners { get; set; } = [];
    public Guid TestOrganizationId { get; set; }

    public bool IsTestOrganizationOwner(string email) =>
        Enabled && TestOrganizationOwners.Any(entry => EmailAddress.Normalize(entry) == EmailAddress.Normalize(email));
}
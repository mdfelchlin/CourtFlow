using System.Text.RegularExpressions;

namespace CourtFlow.Domain;

public static partial class EmailAddress
{
    public const int MaxLength = 254;

    /// <summary>Trimmed and lowercased, as WorkOS compares addresses. Every stored or compared address goes through here.</summary>
    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static bool IsValid(string? email)
    {
        var normalized = Normalize(email);
        return normalized.Length is > 0 and <= MaxLength && Shape().IsMatch(normalized);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Shape();
}

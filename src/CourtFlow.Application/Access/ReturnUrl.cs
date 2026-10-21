namespace CourtFlow.Application.Access;

public static class ReturnUrl
{
    /// <summary>Only a path on the CourtFlow site: it starts with one slash and carries no scheme or host.</summary>
    public static bool IsLocal(string? value) =>
        !string.IsNullOrEmpty(value)
        && value[0] == '/'
        && !value.StartsWith("//", StringComparison.Ordinal)
        && !value.StartsWith("/\\", StringComparison.Ordinal)
        && !value.Contains("://", StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
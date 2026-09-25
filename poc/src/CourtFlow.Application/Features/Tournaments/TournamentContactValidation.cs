namespace CourtFlow.Application.Features.Tournaments;

internal static class TournamentContactValidation
{
    public static bool IsValidWebsiteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        var trimmed = url.Trim();
        var candidate = trimmed.Contains("://", StringComparison.Ordinal)
            ? trimmed
            : $"https://{trimmed}";

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && !string.IsNullOrWhiteSpace(uri.Host);
    }
}

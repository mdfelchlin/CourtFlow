using System.Net.Http.Json;
using CourtFlow.Application.Access;
using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.Captcha;

public sealed class TurnstileOptions
{
    public string SecretKey { get; set; } = "";
}

/// <summary>Checks a Cloudflare Turnstile answer. Anything other than a clear yes counts as a failed check.</summary>
public sealed class TurnstileVerifier(HttpClient http, TurnstileOptions options, ILogger<TurnstileVerifier> logger) : ICaptchaVerifier
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            using var response = await http.PostAsync(VerifyUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = options.SecretKey,
                ["response"] = token,
            }), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<SiteVerify>(cancellationToken);
            return result?.Success == true;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Turnstile could not be reached.");
            return false;
        }
    }

    private sealed record SiteVerify(bool Success);
}

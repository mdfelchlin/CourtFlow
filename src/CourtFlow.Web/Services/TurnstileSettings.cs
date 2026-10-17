namespace CourtFlow.Web.Services;

/// <summary>Cloudflare Turnstile's public site key. The secret key stays on the API.</summary>
public sealed record TurnstileSettings(string SiteKey);

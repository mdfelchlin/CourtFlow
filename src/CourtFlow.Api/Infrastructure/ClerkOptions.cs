namespace CourtFlow.Api.Infrastructure;

public sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    public bool Enabled { get; init; }
    public string? Authority { get; init; }
    public string? Audience { get; init; }
}

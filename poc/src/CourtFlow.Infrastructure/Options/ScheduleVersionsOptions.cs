namespace CourtFlow.Infrastructure.Options;

public sealed class ScheduleVersionsOptions
{
    public const string SectionName = "ScheduleVersions";

    /// <summary>HMAC signing key — must be overridden in shared environments.</summary>
    public string SigningKey { get; set; } = "";
}

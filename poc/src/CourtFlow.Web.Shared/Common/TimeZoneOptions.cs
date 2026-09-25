namespace CourtFlow.Web.Shared.Common;

public sealed record TimeZoneOption(string Id, string Label);

public static class TimeZoneOptions
{
    public const string DefaultId = "America/Los_Angeles";

    public static readonly IReadOnlyList<TimeZoneOption> All = new[]
    {
        new TimeZoneOption("America/Los_Angeles", "PST - Pacific Time"),
        new TimeZoneOption("America/Denver",      "MST - Mountain Time"),
        new TimeZoneOption("America/Phoenix",     "MST - Arizona (no DST)"),
        new TimeZoneOption("America/Chicago",     "CST - Central Time"),
        new TimeZoneOption("America/New_York",    "EST - Eastern Time"),
        new TimeZoneOption("America/Anchorage",   "AKST - Alaska"),
        new TimeZoneOption("Pacific/Honolulu",    "HST - Hawaii"),
        new TimeZoneOption("UTC",                 "UTC"),
    };

    public static string Label(string id) =>
        All.FirstOrDefault(o => o.Id == id)?.Label ?? id;
}

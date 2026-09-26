namespace CourtFlow.Domain;

public sealed record TimeZoneOption(string Id, string Label);

public static class UsTimeZones
{
    public static readonly IReadOnlyList<TimeZoneOption> All =
    [
        new("America/New_York", "Eastern"),
        new("America/Chicago", "Central"),
        new("America/Denver", "Mountain"),
        new("America/Phoenix", "Arizona"),
        new("America/Los_Angeles", "Pacific"),
        new("America/Anchorage", "Alaska"),
        new("Pacific/Honolulu", "Hawaii"),
    ];

    public static bool IsAllowed(string? id) =>
        id is not null && All.Any(zone => zone.Id == id);

    public static string Label(string id) =>
        All.FirstOrDefault(zone => zone.Id == id)?.Label ?? id;
}

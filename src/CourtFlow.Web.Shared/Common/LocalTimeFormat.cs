namespace CourtFlow.Web.Shared.Common;

public static class LocalTimeFormat
{
    public const string DefaultMorningStart = "08:00";
    public const string DefaultAfternoonStart = "15:00";

    public static bool IsValidHhMm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length != 5 || trimmed[2] != ':')
            return false;

        return int.TryParse(trimmed.AsSpan(0, 2), out var hour)
               && int.TryParse(trimmed.AsSpan(3, 2), out var minute)
               && hour is >= 0 and <= 23
               && minute is >= 0 and <= 59;
    }

    public static TimeSpan? TryParseHhMm(string? value)
    {
        if (!IsValidHhMm(value))
            return null;

        var trimmed = value!.Trim();
        var hour = int.Parse(trimmed.AsSpan(0, 2));
        var minute = int.Parse(trimmed.AsSpan(3, 2));
        return new TimeSpan(hour, minute, 0);
    }

    public static string? ToHhMm(TimeSpan? time) =>
        time is null ? null : $"{(int)time.Value.TotalHours:D2}:{time.Value.Minutes:D2}";
}

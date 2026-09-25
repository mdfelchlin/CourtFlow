using CourtFlow.Domain.Enums;

namespace CourtFlow.Infrastructure.Scheduling;

internal static class ScheduleStartTimeResolver
{
    public static DateTime ResolveDayAnchor(DateOnly startDate) =>
        startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public static DateTime ResolveStart(
        DateTime dayAnchor,
        string? localTime,
        int fallbackHour,
        int fallbackMinute = 0)
    {
        if (TryParseLocalTime(localTime, out var hour, out var minute))
            return dayAnchor.AddHours(hour).AddMinutes(minute);

        return dayAnchor.AddHours(fallbackHour).AddMinutes(fallbackMinute);
    }

    public static bool TryParseLocalTime(string? value, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length != 5 || trimmed[2] != ':')
            return false;

        return int.TryParse(trimmed.AsSpan(0, 2), out hour)
               && int.TryParse(trimmed.AsSpan(3, 2), out minute)
               && hour is >= 0 and <= 23
               && minute is >= 0 and <= 59;
    }

    public static DateTime ResolveWaveStart(
        DateTime dayAnchor,
        IReadOnlyDictionary<string, string?> waveStartTimes,
        Wave wave)
    {
        var code = wave == Wave.AM ? "AM" : "PM";
        var fallbackHour = wave == Wave.AM ? 8 : 13;
        waveStartTimes.TryGetValue(code, out var configured);
        return ResolveStart(dayAnchor, configured, fallbackHour);
    }
}

using CourtFlow.Web.Theming;
using Microsoft.JSInterop;

namespace CourtFlow.Web.Tests;

public sealed class FakeThemePreferenceStore : IThemePreferenceStore
{
    public string? Cookie { get; set; }
    public bool SystemPrefersDark { get; set; }
    public int Reads { get; private set; }
    public List<ThemeMode> Writes { get; } = [];
    public bool RefuseWrites { get; set; }

    public Task<ThemePreference> ReadAsync()
    {
        Reads++;
        return Task.FromResult(new ThemePreference(Cookie, SystemPrefersDark));
    }

    public Task WriteAsync(ThemeMode mode)
    {
        if (RefuseWrites)
            throw new JSException("Cookies are refused.");

        Writes.Add(mode);
        return Task.CompletedTask;
    }
}

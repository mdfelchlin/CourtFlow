using CourtFlow.Web.Theming;

namespace CourtFlow.Web.Tests;

public class ThemeStateTests
{
    [Theory]
    [InlineData("dark", false, ThemeMode.Dark)]
    [InlineData("dark", true, ThemeMode.Dark)]
    [InlineData("light", false, ThemeMode.Light)]
    [InlineData("light", true, ThemeMode.Light)]
    public void A_valid_cookie_wins_over_the_system(string cookie, bool systemPrefersDark, ThemeMode expected)
    {
        Assert.Equal(expected, ThemeState.Resolve(cookie, systemPrefersDark));
    }

    [Theory]
    [InlineData(null, false, ThemeMode.Light)]
    [InlineData(null, true, ThemeMode.Dark)]
    [InlineData("blue", false, ThemeMode.Light)]
    [InlineData("blue", true, ThemeMode.Dark)]
    [InlineData("", true, ThemeMode.Dark)]
    public void A_missing_or_invalid_cookie_follows_the_system(string? cookie, bool systemPrefersDark, ThemeMode expected)
    {
        Assert.Equal(expected, ThemeState.Resolve(cookie, systemPrefersDark));
    }

    [Theory]
    [InlineData("DARK", ThemeMode.Dark)]
    [InlineData("Light", ThemeMode.Light)]
    public void Cookie_case_is_ignored(string cookie, ThemeMode expected)
    {
        Assert.Equal(expected, ThemeState.Resolve(cookie, systemPrefersDark: expected == ThemeMode.Light));
    }

    [Fact]
    public async Task Initialize_reads_the_store_once_and_never_writes()
    {
        var store = new FakeThemePreferenceStore { Cookie = "dark" };
        var state = new ThemeState(store);

        await state.InitializeAsync();

        Assert.Equal(ThemeMode.Dark, state.Mode);
        Assert.True(state.IsDarkMode);
        Assert.Equal(1, store.Reads);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task Toggle_flips_the_mode_raises_changed_and_writes_the_new_mode()
    {
        var store = new FakeThemePreferenceStore();
        var state = new ThemeState(store);
        await state.InitializeAsync();
        var changes = 0;
        state.Changed += () => changes++;

        await state.ToggleAsync();

        Assert.Equal(ThemeMode.Dark, state.Mode);
        Assert.Equal(1, changes);
        Assert.Equal([ThemeMode.Dark], store.Writes);

        await state.ToggleAsync();

        Assert.Equal(ThemeMode.Light, state.Mode);
        Assert.Equal(2, changes);
        Assert.Equal([ThemeMode.Dark, ThemeMode.Light], store.Writes);
    }

    [Fact]
    public async Task A_refused_cookie_still_leaves_the_new_mode()
    {
        var store = new FakeThemePreferenceStore { RefuseWrites = true };
        var state = new ThemeState(store);
        await state.InitializeAsync();

        await state.ToggleAsync();

        Assert.Equal(ThemeMode.Dark, state.Mode);
        Assert.Empty(store.Writes);
    }
}

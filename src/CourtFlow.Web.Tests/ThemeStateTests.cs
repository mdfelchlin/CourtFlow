using CourtFlow.Web.Theming;

namespace CourtFlow.Web.Tests;

public class ThemeStateTests
{
    [Theory]
    [InlineData("dark", false, ThemeMode.Dark)]
    [InlineData("dark", true, ThemeMode.Dark)]
    [InlineData("light", false, ThemeMode.Light)]
    [InlineData("light", true, ThemeMode.Light)]
    public void AValidCookieWinsOverTheSystem(string cookie, bool systemPrefersDark, ThemeMode expected)
    {
        Assert.Equal(expected, ThemeState.Resolve(cookie, systemPrefersDark));
    }

    [Theory]
    [InlineData(null, false, ThemeMode.Light)]
    [InlineData(null, true, ThemeMode.Dark)]
    [InlineData("blue", false, ThemeMode.Light)]
    [InlineData("blue", true, ThemeMode.Dark)]
    [InlineData("", true, ThemeMode.Dark)]
    public void AMissingOrInvalidCookieFollowsTheSystem(string? cookie, bool systemPrefersDark, ThemeMode expected)
    {
        Assert.Equal(expected, ThemeState.Resolve(cookie, systemPrefersDark));
    }

    [Theory]
    [InlineData("DARK", ThemeMode.Dark)]
    [InlineData("Light", ThemeMode.Light)]
    public void CookieCaseIsIgnored(string cookie, ThemeMode expected)
    {
        Assert.Equal(expected, ThemeState.Resolve(cookie, systemPrefersDark: expected == ThemeMode.Light));
    }

    [Fact]
    public async Task InitializeReadsTheStoreOnceAndNeverWrites()
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
    public async Task ToggleFlipsTheModeRaisesChangedAndWritesTheNewMode()
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
    public async Task ThePublicAndDirectorChoicesToggleIndependently()
    {
        var publicStore = new FakeThemePreferenceStore { Cookie = "dark" };
        var directorStore = new FakeThemePreferenceStore();
        var publicTheme = new PublicThemeState(publicStore);
        var directorTheme = new ThemeState(directorStore);
        await publicTheme.InitializeAsync();
        await directorTheme.InitializeAsync();

        await publicTheme.ToggleAsync();

        Assert.Equal(ThemeMode.Light, publicTheme.Mode);
        Assert.Equal([ThemeMode.Light], publicStore.Writes);
        Assert.Equal(ThemeMode.Light, directorTheme.Mode);
        Assert.Empty(directorStore.Writes);

        await directorTheme.ToggleAsync();

        Assert.Equal(ThemeMode.Dark, directorTheme.Mode);
        Assert.Equal([ThemeMode.Dark], directorStore.Writes);
        Assert.Equal(ThemeMode.Light, publicTheme.Mode);
        Assert.Equal([ThemeMode.Light], publicStore.Writes);
    }

    [Fact]
    public async Task ARefusedCookieStillLeavesTheNewMode()
    {
        var store = new FakeThemePreferenceStore { RefuseWrites = true };
        var state = new ThemeState(store);
        await state.InitializeAsync();

        await state.ToggleAsync();

        Assert.Equal(ThemeMode.Dark, state.Mode);
        Assert.Empty(store.Writes);
    }
}
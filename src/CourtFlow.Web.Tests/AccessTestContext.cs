using AngleSharp.Dom;

using Bunit;

using CourtFlow.Web.Services;
using CourtFlow.Web.Theming;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

/// <summary>The services every access page and component needs, with the fake API and a popover and dialog host.</summary>
public abstract class AccessTestContext : BunitContext, IDisposable
{
    protected FakeApi Api { get; } = new();
    protected FakeTournamentMemory Memory { get; } = new();
    protected IRenderedComponent<MudPopoverProvider> Popovers { get; private set; } = default!;
    protected IRenderedComponent<MudDialogProvider> Dialogs { get; private set; } = default!;

    protected AccessTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<ICourtFlowApi>(Api);
        Services.AddSingleton<ITournamentMemory>(Memory);
        Services.AddScoped<Feedback>();
        Services.AddScoped<Confirmations>();
        Services.AddSingleton(new ThemeState(new FakeThemePreferenceStore()));
        Services.AddSingleton(new PublicThemeState(new FakeThemePreferenceStore()));
        this.AddAccessServices();
    }

    /// <summary>Signs in, then renders the popover and dialog hosts. Services are fixed after the first render.</summary>
    protected void SignInAs(MeResponse me)
    {
        this.SignIn(me);
        Popovers = Render<MudPopoverProvider>();
        Dialogs = Render<MudDialogProvider>();
    }

    void IDisposable.Dispose()
    {
        Services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    protected NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    protected IElement DialogButton(string text) =>
        Dialogs.FindAll("button").Single(button => button.TextContent.Trim() == text);

    protected void Confirm(string confirmText)
    {
        Dialogs.WaitForAssertion(() => Assert.Contains(Dialogs.FindAll("button"), button => button.TextContent.Trim() == confirmText));
        DialogButton(confirmText).Click();
    }

    protected bool Snackbar(string message) =>
        Services.GetRequiredService<ISnackbar>().ShownSnackbars.Any(snackbar => snackbar.Message == message);
}
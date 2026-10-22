using System.Globalization;

using AngleSharp.Dom;

using Bunit;

using CourtFlow.Web;
using CourtFlow.Web.Components;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

namespace CourtFlow.Web.Tests;

public class WrapperTests : BunitContext, IDisposable
{
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;

    public WrapperTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _popovers = Render<MudPopoverProvider>();
    }

    void IDisposable.Dispose()
    {
        Services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TextFieldRendersAPlainLabelTiedToTheInput()
    {
        var cut = Render<CfTextField>(parameters => parameters.Add(field => field.Label, "Name"));

        AssertLabelTiedToInput(cut.Nodes, "Name");
    }

    [Fact]
    public void NumericFieldRendersAPlainLabelTiedToTheInput()
    {
        var cut = Render<CfNumericField<int?>>(parameters => parameters
            .Add(field => field.Label, "Count")
            .Add(field => field.Min, 0)
            .Add(field => field.Max, 99));

        AssertLabelTiedToInput(cut.Nodes, "Count");
    }

    [Fact]
    public void DateFieldRendersAPlainLabelTiedToTheInput()
    {
        var cut = Render<CfDateField>(parameters => parameters.Add(field => field.Label, "Start date"));

        AssertLabelTiedToInput(cut.Nodes, "Start date");
    }

    [Fact]
    public void SelectRendersAPlainLabelTiedToTheInput()
    {
        var cut = RenderSelect();

        AssertLabelTiedToInput(cut.Nodes, "Time zone");
    }

    [Fact]
    public void RequiredFieldsShowAnAsteriskAndMarkTheInputRequired()
    {
        var text = Render<CfTextField>(parameters => parameters.Add(field => field.Label, "Name").Add(field => field.Required, true));
        var date = Render<CfDateField>(parameters => parameters.Add(field => field.Label, "Start").Add(field => field.Required, true));

        foreach (var cut in new[] { text.Nodes, date.Nodes })
        {
            Assert.Contains("*", cut.QuerySelector("label")!.TextContent);
            Assert.True(cut.QuerySelector("input")!.HasAttribute("required"));
        }
    }

    [Fact]
    public void OptionalFieldsShowNoAsterisk()
    {
        var cut = Render<CfTextField>(parameters => parameters.Add(field => field.Label, "Description"));

        Assert.DoesNotContain("*", cut.Find("label").TextContent);
    }

    [Fact]
    public void NumericFieldPassesMinAndMaxThrough()
    {
        var cut = Render<CfNumericField<int?>>(parameters => parameters
            .Add(field => field.Label, "Duration")
            .Add(field => field.Min, 15)
            .Add(field => field.Max, 240));

        var numeric = cut.FindComponent<MudNumericField<int?>>().Instance;
        Assert.Equal(15, numeric.Min);
        Assert.Equal(240, numeric.Max);
    }

    [Fact]
    public void DateFieldShowsAUsDateAndThePlaceholderWhenEmpty()
    {
        var filled = RenderDateField(new DateOnly(2026, 9, 3));
        Assert.Equal("09/03/2026", filled.Find("input").GetAttribute("value"));

        var empty = RenderDateField(null);
        Assert.Equal(CalendarDateFormat.Placeholder, empty.Find("input").GetAttribute("placeholder"));
    }

    [Theory]
    [InlineData("09/01/2026")]
    [InlineData("9/1/2026")]
    public void DateFieldAcceptsAUsDate(string typed)
    {
        DateOnly? changed = null;
        var cut = RenderDateField(null, value => changed = value);

        cut.Find("input").Change(typed);

        cut.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 9, 1), changed));
        Assert.Equal("09/01/2026", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void DateFieldReadsMonthBeforeDay()
    {
        DateOnly? changed = null;
        var cut = RenderDateField(null, value => changed = value);

        cut.Find("input").Change("02/03/2026");

        cut.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 2, 3), changed));
    }

    [Theory]
    [InlineData("13/01/2026")]
    [InlineData("02/31/2026")]
    [InlineData("09/01/26")]
    [InlineData("2026-09-01")]
    public void DateFieldRejectsADateThatIsNotARealUsDate(string typed)
    {
        var accepted = new List<DateOnly?>();
        var cut = RenderDateField(null, value => accepted.Add(value));

        cut.Find("input").Change(typed);

        Assert.DoesNotContain(accepted, value => value is not null);
    }

    [Fact]
    public void DateFieldClearsWhenTheInputIsEmptied()
    {
        DateOnly? changed = new DateOnly(2026, 9, 3);
        var cut = RenderDateField(changed, value => changed = value);

        cut.Find("input").Change("");

        cut.WaitForAssertion(() => Assert.Null(changed));
    }

    [Fact]
    public void DateFieldKeepsUsOrderWhenTheCultureIsDayFirst()
    {
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("09/01/2026", CalendarDateFormat.Display(new DateOnly(2026, 9, 1)));

            DateOnly? changed = null;
            var cut = RenderDateField(new DateOnly(2026, 9, 1), value => changed = value);

            Assert.Equal("09/01/2026", cut.Find("input").GetAttribute("value"));

            cut.Find("input").Change("02/03/2026");

            cut.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 2, 3), changed));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    [Fact]
    public async Task DateFieldShowsADateChosenOnThePicker()
    {
        DateOnly? changed = null;
        var cut = RenderDateField(null, value => changed = value);

        var picker = cut.FindComponent<MudDatePicker>();
        await cut.InvokeAsync(() => picker.Instance.DateChanged.InvokeAsync(new DateTime(2026, 9, 1)));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new DateOnly(2026, 9, 1), changed);
            Assert.Equal("09/01/2026", cut.Find("input").GetAttribute("value"));
        });
    }

    [Fact]
    public void AutocompleteRendersAPlainLabelTiedToTheInput()
    {
        var cut = Render<CfAutocomplete<string>>(parameters => parameters
            .Add(field => field.Label, "Club")
            .Add(field => field.SearchFunc, (_, _) => Task.FromResult<IEnumerable<string>>([])));

        AssertLabelTiedToInput(cut.Nodes, "Club");
    }

    [Fact]
    public void FileDropZoneRendersALabelAndABrowseButton()
    {
        var cut = Render<CfFileDropZone>(parameters => parameters
            .Add(field => field.Label, "CSV file")
            .Add(field => field.Required, true));

        Assert.Contains("CSV file", cut.Find("label").TextContent);
        Assert.Contains("*", cut.Find("label").TextContent);
        Assert.NotNull(cut.Find("[data-testid=browse-file]"));
    }

    [Fact]
    public void SwitchRendersItsLabel()
    {
        var cut = Render<CfSwitch>(parameters => parameters.Add(field => field.Label, "Rejected only"));

        Assert.Contains("Rejected only", cut.Markup);
    }

    [Fact]
    public async Task SelectShowsItsPlaceholderWhenEmptyAndListsLabels()
    {
        var cut = RenderSelect();

        Assert.Equal("Select a time zone", cut.Find("input").GetAttribute("placeholder"));

        var select = cut.FindComponent<MudSelect<string>>();
        await cut.InvokeAsync(() => select.Instance.OpenMenu());

        _popovers.WaitForAssertion(() =>
        {
            Assert.Contains("Central", _popovers.Markup);
            Assert.Contains("Pacific", _popovers.Markup);
        });
    }

    [Fact]
    public async Task SaveButtonDisablesWhileSavingAndRunsOnceForTwoClicks()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var cut = Render<CfSaveButton>(parameters => parameters
            .Add(button => button.Text, "Save")
            .Add(button => button.OnSave, () => { calls++; return gate.Task; }));

        cut.Find("button").Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find("button").HasAttribute("disabled")));
        await cut.InvokeAsync(() => cut.FindComponent<MudButton>().Instance.OnClick.InvokeAsync());

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.False(cut.Find("button").HasAttribute("disabled")));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void IconButtonUsesItsLabelForAriaAndTooltip()
    {
        var cut = Render<CfIconButton>(parameters => parameters
            .Add(button => button.Icon, Icons.Material.Filled.Edit)
            .Add(button => button.Label, "Edit City Cup"));

        Assert.Equal("Edit City Cup", cut.Find("button").GetAttribute("aria-label"));
        Assert.Equal("Edit City Cup", cut.FindComponent<MudTooltip>().Instance.Text);
    }

    [Fact]
    public void ADisabledIconButtonKeepsItsLabelAndSaysWhyInTheTooltip()
    {
        var cut = Render<CfIconButton>(parameters => parameters
            .Add(button => button.Icon, Icons.Material.Filled.Delete)
            .Add(button => button.Label, "Delete City Cup")
            .Add(button => button.Tooltip, "Unpublish or archive this tournament before deleting it.")
            .Add(button => button.Disabled, true));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
        Assert.Equal("Delete City Cup", cut.Find("button").GetAttribute("aria-label"));
        Assert.Equal("Unpublish or archive this tournament before deleting it.", cut.FindComponent<MudTooltip>().Instance.Text);
    }

    [Fact]
    public void PageHeaderRendersExactlyOneH1()
    {
        var cut = Render<CfPageHeader>(parameters => parameters
            .Add(header => header.Title, "Tournaments")
            .Add(header => header.BackHref, "/tournaments"));

        var headings = cut.FindAll("h1");
        Assert.Single(headings);
        Assert.Equal("Tournaments", headings[0].TextContent.Trim());
        Assert.NotNull(cut.Find("[aria-label='Back to tournaments']"));
    }

    private IRenderedComponent<CfDateField> RenderDateField(DateOnly? initial, Action<DateOnly?>? onChanged = null) =>
        Render<CfDateField>(parameters => parameters
            .Add(field => field.Label, "Start date")
            .Bind(field => field.Value, initial, onChanged ?? (_ => { })));

    private IRenderedComponent<CfSelect<string>> RenderSelect() =>
        Render<CfSelect<string>>(parameters => parameters
            .Add(select => select.Label, "Time zone")
            .Add(select => select.Items, ["America/Chicago", "America/Los_Angeles"])
            .Add(select => select.ToLabel, id => id == "America/Chicago" ? "Central" : "Pacific")
            .Add(select => select.Placeholder, "Select a time zone"));

    private static void AssertLabelTiedToInput(INodeList nodes, string labelText)
    {
        var label = nodes.QuerySelector("label");
        var input = nodes.QuerySelector("input");
        Assert.NotNull(label);
        Assert.NotNull(input);
        Assert.Contains(labelText, label.TextContent);

        // The label comes before the control in reading order.
        Assert.True(label.CompareDocumentPosition(input).HasFlag(DocumentPositions.Following));

        var tied = label.GetAttribute("for") is { } forId && forId == input.GetAttribute("id")
                   || input.GetAttribute("aria-labelledby") is { } labelledBy && labelledBy == label.GetAttribute("id");
        Assert.True(tied, "The label is not tied to the input by for/id or aria-labelledby.");

        // No floating MudBlazor label.
        Assert.Null(nodes.QuerySelector(".mud-input-label"));
    }
}
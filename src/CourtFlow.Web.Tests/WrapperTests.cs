using AngleSharp.Dom;
using Bunit;
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

    void IDisposable.Dispose() => Services.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Fact]
    public void Text_field_renders_a_plain_label_tied_to_the_input()
    {
        var cut = Render<CfTextField>(parameters => parameters.Add(field => field.Label, "Name"));

        AssertLabelTiedToInput(cut.Nodes, "Name");
    }

    [Fact]
    public void Numeric_field_renders_a_plain_label_tied_to_the_input()
    {
        var cut = Render<CfNumericField<int?>>(parameters => parameters
            .Add(field => field.Label, "Count")
            .Add(field => field.Min, 0)
            .Add(field => field.Max, 99));

        AssertLabelTiedToInput(cut.Nodes, "Count");
    }

    [Fact]
    public void Date_field_renders_a_plain_label_tied_to_the_input()
    {
        var cut = Render<CfDateField>(parameters => parameters.Add(field => field.Label, "Start date"));

        AssertLabelTiedToInput(cut.Nodes, "Start date");
    }

    [Fact]
    public void Select_renders_a_plain_label_tied_to_the_input()
    {
        var cut = RenderSelect();

        AssertLabelTiedToInput(cut.Nodes, "Time zone");
    }

    [Fact]
    public void Required_fields_show_an_asterisk_and_mark_the_input_required()
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
    public void Optional_fields_show_no_asterisk()
    {
        var cut = Render<CfTextField>(parameters => parameters.Add(field => field.Label, "Description"));

        Assert.DoesNotContain("*", cut.Find("label").TextContent);
    }

    [Fact]
    public void Numeric_field_passes_min_and_max_through()
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
    public void Date_field_shows_its_value_and_accepts_a_typed_date()
    {
        DateOnly? changed = null;
        var cut = Render<CfDateField>(parameters => parameters
            .Add(field => field.Label, "Start date")
            .Add(field => field.Value, new DateOnly(2026, 9, 3))
            .Add(field => field.ValueChanged, value => changed = value));

        Assert.Equal("2026-09-03", cut.Find("input").GetAttribute("value"));

        cut.Find("input").Change("2026-09-01");

        cut.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 9, 1), changed));
    }

    [Fact]
    public async Task Select_shows_its_placeholder_when_empty_and_lists_labels()
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
    public async Task Save_button_disables_while_saving_and_runs_once_for_two_clicks()
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
    public void Icon_button_uses_its_label_for_aria_and_tooltip()
    {
        var cut = Render<CfIconButton>(parameters => parameters
            .Add(button => button.Icon, Icons.Material.Filled.Edit)
            .Add(button => button.Label, "Edit City Cup"));

        Assert.Equal("Edit City Cup", cut.Find("button").GetAttribute("aria-label"));
        Assert.Equal("Edit City Cup", cut.FindComponent<MudTooltip>().Instance.Text);
    }

    [Fact]
    public void Page_header_renders_exactly_one_h1()
    {
        var cut = Render<CfPageHeader>(parameters => parameters
            .Add(header => header.Title, "Tournaments")
            .Add(header => header.BackHref, "/"));

        var headings = cut.FindAll("h1");
        Assert.Single(headings);
        Assert.Equal("Tournaments", headings[0].TextContent.Trim());
        Assert.NotNull(cut.Find("[aria-label='Back to tournaments']"));
    }

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

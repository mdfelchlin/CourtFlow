using Bunit;

using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;

using MudBlazor;

namespace CourtFlow.Web.Tests;

public class RequestAccessPageTests : AccessTestContext
{
    [Fact]
    public void ThePageExplainsItselfAndLinksBackToSignIn()
    {
        var cut = RenderPage();

        Assert.Equal("Request Access", cut.Find("h1").TextContent.Trim());
        Assert.Contains(RequestAccess.Intro, cut.Markup);
        Assert.Equal(TestAccess.Login("/tournaments"), cut.Find("[data-testid=back-to-sign-in]").GetAttribute("href"));
    }

    [Fact]
    public void RequiredFieldsAreCheckedBeforeSending()
    {
        var cut = RenderPage();

        cut.Find("[data-testid=send-request]").Click();

        cut.WaitForAssertion(() => Assert.Contains(AccessRequest.NameRequired, cut.Markup));
        Assert.Contains(AccessRequest.EmailRequired, cut.Markup);
        Assert.Contains(AccessRequest.OrganizationNameRequired, cut.Markup);
        Assert.Null(Api.LastAccessRequest);
    }

    [Fact]
    public void LengthLimitsAndEmailShapeAreChecked()
    {
        var cut = RenderPage();

        Fill(cut, "request-name", new string('n', 101));
        Fill(cut, "request-email", "not-an-email");
        Fill(cut, "request-organization", new string('o', 101));
        Fill(cut, "request-message", new string('m', 1001));
        cut.Find("[data-testid=send-request]").Click();

        cut.WaitForAssertion(() => Assert.Contains(AccessRequest.NameTooLong, cut.Markup));
        Assert.Contains(AccessRequest.EmailInvalid, cut.Markup);
        Assert.Contains(AccessRequest.OrganizationNameTooLong, cut.Markup);
        Assert.Contains(AccessRequest.MessageTooLong, cut.Markup);
        Assert.Null(Api.LastAccessRequest);
    }

    [Fact]
    public void SendingIncludesTheTurnstileTokenAndSuccessReplacesTheForm()
    {
        Api.AccessRequestResult = new AccessRequestResultResponse(AccessRequestOutcome.Sent, "Thanks — we'll email you when your account is ready.");
        var cut = RenderPage();
        FillValid(cut);
        cut.InvokeAsync(() => cut.FindComponent<CfTurnstile>().Instance.OnToken("turnstile-token"));

        cut.Find("[data-testid=send-request]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=request-sent]")));
        Assert.Contains("Thanks — we'll email you when your account is ready.", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=send-request]"));
        Assert.Equal("turnstile-token", Api.LastAccessRequest!.CaptchaToken);
        Assert.Equal("Sam Setter", Api.LastAccessRequest.Name);
        Assert.Equal("sam@club.test", Api.LastAccessRequest.Email);
        Assert.Equal("Rapids VBC", Api.LastAccessRequest.OrganizationName);
    }

    [Theory]
    [InlineData(AccessRequestOutcome.CaptchaFailed, "Please complete the check and try again.")]
    [InlineData(AccessRequestOutcome.AlreadyHasAccess, "This email already has access. Sign in instead.")]
    [InlineData(AccessRequestOutcome.AlreadyWaiting, "There's already a request waiting for this email.")]
    [InlineData(AccessRequestOutcome.Declined, "This email can't send a new request. Contact mdfelchlin@gmail.com.")]
    [InlineData(AccessRequestOutcome.CouldNotSend, "We couldn't send your request. Please try again in a few minutes.")]
    public void EachRefusalShowsItsMessageAndKeepsTheForm(AccessRequestOutcome outcome, string message)
    {
        Api.AccessRequestResult = new AccessRequestResultResponse(outcome, message);
        var cut = RenderPage();
        FillValid(cut);

        cut.Find("[data-testid=send-request]").Click();

        cut.WaitForAssertion(() => Assert.Equal(message, cut.Find("[data-testid=request-outcome]").TextContent.Trim()));
        Assert.NotNull(cut.Find("[data-testid=send-request]"));
        Assert.Equal("Sam Setter", cut.Find("[data-testid=request-name] input").GetAttribute("value"));
        Assert.Equal(outcome == AccessRequestOutcome.AlreadyHasAccess, cut.FindAll("[data-testid=outcome-login]").Count == 1);
    }

    [Fact]
    public void AFailedCallShowsCouldNotSend()
    {
        var cut = RenderPage();
        FillValid(cut);
        Api.FailWith = "offline";

        cut.Find("[data-testid=send-request]").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            RequestAccess.CouldNotSendMessage,
            cut.Find("[data-testid=request-outcome]").TextContent.Trim()));
    }

    [Fact]
    public void SignedInTheEmailIsFilledAndReadOnly()
    {
        SignInAs(TestAccess.NoMembership());
        var cut = Render<RequestAccess>();

        var email = cut.Find("[data-testid=request-email] input");
        Assert.Equal("nora@club.test", email.GetAttribute("value"));
        Assert.True(email.HasAttribute("readonly"));
    }

    private IRenderedComponent<RequestAccess> RenderPage()
    {
        SignInAs(TestAccess.SignedOut);
        return Render<RequestAccess>();
    }

    private static void FillValid(IRenderedComponent<RequestAccess> cut)
    {
        Fill(cut, "request-name", "Sam Setter");
        Fill(cut, "request-email", "sam@club.test");
        Fill(cut, "request-organization", "Rapids VBC");
    }

    private static void Fill(IRenderedComponent<RequestAccess> cut, string testId, string value)
    {
        var field = cut.Find($"[data-testid={testId}] input, [data-testid={testId}] textarea");
        field.Change(value);
    }
}
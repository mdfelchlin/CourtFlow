using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class AccessRequestRulesTests
{
    private static readonly DateTimeOffset Requested = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_request_is_trimmed_waiting_and_expires_in_7_days()
    {
        var request = AccessRequest.Create("wl_1", "  Pat Lee ", " Pat@Example.com ", "  Rapids ", "  Hello ", Requested);

        Assert.Equal("Pat Lee", request.Name);
        Assert.Equal("pat@example.com", request.Email);
        Assert.Equal("Rapids", request.OrganizationName);
        Assert.Equal("Hello", request.Message);
        Assert.Equal(AccessRequestState.Waiting, request.State);
        Assert.Equal(Requested.AddDays(7), request.ExpiresAt);
    }

    [Theory]
    [InlineData("", "pat@example.com", "Rapids", null, AccessRequest.NameRequired)]
    [InlineData("   ", "pat@example.com", "Rapids", null, AccessRequest.NameRequired)]
    [InlineData("Pat", "", "Rapids", null, AccessRequest.EmailRequired)]
    [InlineData("Pat", "pat@", "Rapids", null, AccessRequest.EmailInvalid)]
    [InlineData("Pat", "pat example@x.com", "Rapids", null, AccessRequest.EmailInvalid)]
    [InlineData("Pat", "pat@example.com", "", null, AccessRequest.OrganizationNameRequired)]
    public void Required_fields_and_email_shape_are_checked(string name, string email, string organization, string? message, string expected)
    {
        Assert.Equal(expected, AccessRequest.Problem(name, email, organization, message));
        Assert.Equal(expected, Assert.Throws<DomainRuleException>(() => AccessRequest.Validate(name, email, organization, message)).Message);
    }

    [Fact]
    public void Lengths_are_limited()
    {
        Assert.Null(AccessRequest.Problem(new string('a', 100), "pat@example.com", new string('b', 100), new string('c', 1000)));
        Assert.Equal(AccessRequest.NameTooLong, AccessRequest.Problem(new string('a', 101), "pat@example.com", "Rapids", null));
        Assert.Equal(AccessRequest.OrganizationNameTooLong, AccessRequest.Problem("Pat", "pat@example.com", new string('b', 101), null));
        Assert.Equal(AccessRequest.MessageTooLong, AccessRequest.Problem("Pat", "pat@example.com", "Rapids", new string('c', 1001)));

        var longEmail = new string('a', 243) + "@example.com";
        Assert.Equal(255, longEmail.Length);
        Assert.Equal(AccessRequest.EmailInvalid, AccessRequest.Problem("Pat", longEmail, "Rapids", null));
    }

    [Fact]
    public void The_reminder_is_due_24_hours_before_expiry()
    {
        var request = Create();

        Assert.False(request.ReminderDue(Requested.AddDays(6).AddTicks(-1)));
        Assert.True(request.ReminderDue(Requested.AddDays(6)));
    }

    [Fact]
    public void The_reminder_is_due_only_once()
    {
        var request = Create();

        request.MarkReminderSent(Requested.AddDays(6));

        Assert.False(request.ReminderDue(Requested.AddDays(6).AddHours(1)));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("decline")]
    public void No_reminder_after_a_decision(string action)
    {
        var request = Create();

        if (action == "approve")
            request.Approve();
        else
            request.Decline();

        Assert.False(request.ReminderDue(Requested.AddDays(6)));
        Assert.False(request.ExpiryDue(Requested.AddDays(8)));
    }

    [Fact]
    public void Expiry_is_due_at_7_days()
    {
        var request = Create();

        Assert.False(request.ExpiryDue(Requested.AddDays(7).AddTicks(-1)));
        Assert.True(request.ExpiryDue(Requested.AddDays(7)));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("decline")]
    [InlineData("expire")]
    [InlineData("remind")]
    public void Every_change_refuses_a_request_that_is_not_waiting(string action)
    {
        var request = Create();
        request.Expire();

        var error = Assert.Throws<DomainRuleException>(() =>
        {
            switch (action)
            {
                case "approve": request.Approve(); break;
                case "decline": request.Decline(); break;
                case "expire": request.Expire(); break;
                default: request.MarkReminderSent(Requested); break;
            }
        });

        Assert.Equal(AccessRequest.NotWaiting, error.Message);
    }

    private static AccessRequest Create() =>
        AccessRequest.Create("wl_1", "Pat", "pat@example.com", "Rapids", null, Requested);
}

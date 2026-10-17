using System.Net;
using System.Text.RegularExpressions;
using CourtFlow.Application.Email;
using CourtFlow.Domain;

namespace CourtFlow.Application.Tests;

public sealed class EmailTemplatesTests
{
    private const string Link = "https://courtflow.test/accept?x=1&y=2";
    private static readonly DateTimeOffset Requested = new(2026, 10, 13, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expires = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    private static AccessRequest Request(string? message = "We run 20 events.") =>
        AccessRequest.Create("wle_1", "Casey Jones", "casey@club.test", "River City VBC", message, Requested);

    public static TheoryData<EmailMessage, string> Emails() => new()
    {
        { EmailTemplates.NewAccessRequest(["op@courtflow.test"], Request(), Link), "CourtFlow access request from Casey Jones" },
        { EmailTemplates.RequestExpiring(Request(), Link), "Your CourtFlow access request is still waiting" },
        { EmailTemplates.ApprovedNewOrganization("casey@club.test", "Casey Jones", "River City VBC", Link, Expires), "Your CourtFlow account is ready" },
        { EmailTemplates.ApprovedExistingOrganization("casey@club.test", "Casey Jones", "River City VBC", OrganizationRole.Editor, Link, Expires), "You've been added to River City VBC on CourtFlow" },
        { EmailTemplates.Declined("casey@club.test", "Casey Jones", "River City VBC"), "Your CourtFlow access request" },
        { EmailTemplates.OwnerInvitation("casey@club.test", "Pat Owner", "River City VBC", OrganizationRole.Viewer, Link, Expires), "Pat Owner invited you to River City VBC on CourtFlow" },
        { EmailTemplates.OperatorInvitation("casey@club.test", "River City VBC", Link, Expires), "You're the owner of River City VBC on CourtFlow" },
        { EmailTemplates.Deactivated("casey@club.test", "Casey Jones", "River City VBC"), "River City VBC has been deactivated on CourtFlow" },
        { EmailTemplates.Reactivated("casey@club.test", "Casey Jones", "River City VBC", Link), "River City VBC is active again on CourtFlow" },
    };

    public static TheoryData<EmailMessage> Bodies() => new(Emails().Select(row => row.Data.Item1));

    [Theory]
    [MemberData(nameof(Emails))]
    public void Each_email_has_its_subject_and_no_unfilled_placeholders(EmailMessage email, string subject)
    {
        Assert.Equal(subject, email.Subject);
        Assert.DoesNotContain("{", email.Text);
        Assert.DoesNotContain("}", email.Text);
        Assert.NotEmpty(email.To);
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void Text_and_html_carry_the_same_sentences(EmailMessage email)
    {
        var htmlText = WebUtility.HtmlDecode(Regex.Replace(email.Html.Replace("<br>", "\n").Replace("</p>", "\n\n"), "<[^>]+>", ""));

        Assert.Equal(Normalize(email.Text), Normalize(htmlText));
    }

    [Fact]
    public void Links_become_anchors_in_html()
    {
        var email = EmailTemplates.ApprovedNewOrganization("casey@club.test", "Casey", "River City VBC", Link, Expires);

        Assert.Contains("<a href=\"https://courtflow.test/accept?x=1&amp;y=2\">", email.Html);
        Assert.Contains(Link, email.Text);
    }

    [Fact]
    public void Dates_read_like_October_20_2026()
    {
        var email = EmailTemplates.ApprovedNewOrganization("casey@club.test", "Casey", "River City VBC", Link, Expires);

        Assert.Contains("This link expires on October 20, 2026.", email.Text);
        Assert.Equal("October 20, 2026", EmailTemplates.Date(Expires));
    }

    [Theory]
    [InlineData(OrganizationRole.Editor, "as an editor")]
    [InlineData(OrganizationRole.Viewer, "as a viewer")]
    [InlineData(OrganizationRole.Owner, "as an owner")]
    public void Roles_read_with_their_article(OrganizationRole role, string expected)
    {
        var email = EmailTemplates.OwnerInvitation("casey@club.test", "Pat", "River City VBC", role, Link, Expires);

        Assert.Contains(expected, email.Text);
    }

    [Fact]
    public void The_contact_address_appears_in_emails_2_5_and_8()
    {
        Assert.Contains(CourtFlowContact.Email, EmailTemplates.RequestExpiring(Request(), Link).Text);
        Assert.Contains(CourtFlowContact.Email, EmailTemplates.Declined("casey@club.test", "Casey", "River City VBC").Text);
        Assert.Contains(CourtFlowContact.Email, EmailTemplates.Deactivated("casey@club.test", "Casey", "River City VBC").Text);
    }

    [Fact]
    public void The_new_request_email_lists_the_request()
    {
        var email = EmailTemplates.NewAccessRequest(["op@courtflow.test"], Request(message: null), Link);

        Assert.Contains("Casey Jones (casey@club.test) has asked for access to CourtFlow.", email.Text);
        Assert.Contains("Organization: River City VBC", email.Text);
        Assert.Contains("Message: None", email.Text);
        Assert.Contains("Requested: October 13, 2026", email.Text);
        Assert.Contains("Expires: October 20, 2026", email.Text);
    }

    private static string Normalize(string value) => Regex.Replace(value, @"\s+", " ").Trim();
}

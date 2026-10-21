using System.Globalization;
using System.Net;

using CourtFlow.Domain;

namespace CourtFlow.Application.Email;

/// <summary>The intent's nine emails. Each has a plain-text body and an HTML body with the same sentences.</summary>
public static class EmailTemplates
{
    private const string SignOff = "The CourtFlow team";
    private const string SignInWith = "Sign in with this email address, using a password or Google:";

    private static readonly CultureInfo UsEnglish = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Dates only, like "October 20, 2026", so no time zone is needed.</summary>
    public static string Date(DateTimeOffset value) => value.UtcDateTime.ToString("MMMM d, yyyy", UsEnglish);

    public static EmailMessage NewAccessRequest(IReadOnlyList<string> operators, AccessRequest request, string operatorRequestsLink) =>
        Build(operators, $"CourtFlow access request from {request.Name}",
            [
                $"{request.Name} ({request.Email}) has asked for access to CourtFlow.",
                $"Organization: {request.OrganizationName}\n" +
                $"Message: {(string.IsNullOrWhiteSpace(request.Message) ? "None" : request.Message)}\n" +
                $"Requested: {Date(request.RequestedAt)}\n" +
                $"Expires: {Date(request.ExpiresAt)}",
                $"Review it here: {operatorRequestsLink}",
            ],
            operatorRequestsLink);

    public static EmailMessage RequestExpiring(AccessRequest request, string landingPageLink) =>
        Build([request.Email], "Your CourtFlow access request is still waiting",
            [
                Greeting(request.Name),
                $"We haven't finished reviewing your request for {request.OrganizationName} yet, and it expires on {Date(request.ExpiresAt)}. If it expires, you can send a new one from {landingPageLink}.",
                $"If you'd like to follow up, reply to {CourtFlowContact.Email}.",
                SignOff,
            ],
            landingPageLink);

    public static EmailMessage ApprovedNewOrganization(string to, string name, string organizationName, string acceptLink, DateTimeOffset expiresAt) =>
        Build([to], "Your CourtFlow account is ready",
            [
                Greeting(name),
                $"Your request was approved. {organizationName} is set up on CourtFlow, and you are its owner.",
                $"{SignInWith} {acceptLink}",
                $"This link expires on {Date(expiresAt)}.",
                SignOff,
            ],
            acceptLink);

    public static EmailMessage ApprovedExistingOrganization(
        string to,
        string name,
        string organizationName,
        OrganizationRole role,
        string acceptLink,
        DateTimeOffset expiresAt) =>
        Build([to], $"You've been added to {organizationName} on CourtFlow",
            [
                Greeting(name),
                $"Your request was approved. You've been added to {organizationName} as {OrganizationRoles.WithArticle(role)}.",
                $"{SignInWith} {acceptLink}",
                $"This link expires on {Date(expiresAt)}.",
                SignOff,
            ],
            acceptLink);

    public static EmailMessage Declined(string to, string name, string organizationName) =>
        Build([to], "Your CourtFlow access request",
            [
                Greeting(name),
                $"Thanks for your interest in CourtFlow. We're not able to set up access for {organizationName} right now.",
                $"If you have questions, contact {CourtFlowContact.Email}.",
                SignOff,
            ]);

    public static EmailMessage OwnerInvitation(
        string to,
        string inviterName,
        string organizationName,
        OrganizationRole role,
        string acceptLink,
        DateTimeOffset expiresAt) =>
        Build([to], $"{inviterName} invited you to {organizationName} on CourtFlow",
            [
                "Hi,",
                $"{inviterName} invited you to join {organizationName} on CourtFlow as {OrganizationRoles.WithArticle(role)}.",
                $"Accept the invitation and sign in with this email address, using a password or Google: {acceptLink}",
                $"This link expires on {Date(expiresAt)}. If you weren't expecting this, you can ignore it.",
                SignOff,
            ],
            acceptLink);

    public static EmailMessage OperatorInvitation(string to, string organizationName, string acceptLink, DateTimeOffset expiresAt) =>
        Build([to], $"You're the owner of {organizationName} on CourtFlow",
            [
                "Hi,",
                $"You've been made an owner of {organizationName} on CourtFlow.",
                $"{SignInWith} {acceptLink}",
                $"This link expires on {Date(expiresAt)}.",
                SignOff,
            ],
            acceptLink);

    public static EmailMessage Deactivated(string to, string name, string organizationName) =>
        Build([to], $"{organizationName} has been deactivated on CourtFlow",
            [
                Greeting(name),
                $"{organizationName} has been deactivated on CourtFlow. You can no longer sign in to it, and its tournaments no longer appear on the public site. Its information has been kept.",
                $"If you think this is a mistake, contact {CourtFlowContact.Email}.",
                SignOff,
            ]);

    public static EmailMessage Reactivated(string to, string name, string organizationName, string signInLink) =>
        Build([to], $"{organizationName} is active again on CourtFlow",
            [
                Greeting(name),
                $"{organizationName} has been reactivated on CourtFlow. You can sign in again at {signInLink}, and its published tournaments are back on the public site.",
                SignOff,
            ],
            signInLink);

    private static string Greeting(string? name) => string.IsNullOrWhiteSpace(name) ? "Hi," : $"Hi {name.Trim()},";

    /// <summary>The text body joins the paragraphs; the HTML body encodes the same paragraphs and turns each known link into an anchor.</summary>
    private static EmailMessage Build(IReadOnlyList<string> to, string subject, IReadOnlyList<string> paragraphs, params string[] links)
    {
        var text = string.Join("\n\n", paragraphs) + "\n";
        var html = string.Concat(paragraphs.Select(paragraph => $"<p>{HtmlParagraph(paragraph, links)}</p>"));
        return new EmailMessage(to, subject, text, $"<!DOCTYPE html><html><body>{html}</body></html>");
    }

    private static string HtmlParagraph(string paragraph, IEnumerable<string> links)
    {
        var encoded = WebUtility.HtmlEncode(paragraph).Replace("\n", "<br>");
        foreach (var link in links.Where(link => !string.IsNullOrEmpty(link)).Distinct())
        {
            var encodedLink = WebUtility.HtmlEncode(link);
            encoded = encoded.Replace(encodedLink, $"<a href=\"{encodedLink}\">{encodedLink}</a>");
        }

        return encoded;
    }
}
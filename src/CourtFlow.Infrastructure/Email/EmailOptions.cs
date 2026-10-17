namespace CourtFlow.Infrastructure.Email;

public sealed class EmailOptions
{
    /// <summary>The sending domain. Until it is set, Resend can only use its test sender.</summary>
    public string Domain { get; set; } = "";
    public string ReplyTo { get; set; } = "";

    /// <summary>An SMTP address such as <c>smtp://localhost:1025</c>. Aspire sets it to Mailpit locally.</summary>
    public string Smtp { get; set; } = "";

    public string From => Domain.Length > 0 ? $"CourtFlow <no-reply@{Domain}>" : "CourtFlow <onboarding@resend.dev>";
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = "";
}

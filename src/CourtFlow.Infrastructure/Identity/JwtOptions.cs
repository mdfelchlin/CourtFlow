namespace CourtFlow.Infrastructure.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CourtFlow";
    public string Audience { get; set; } = "CourtFlow";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 60 * 12;
}

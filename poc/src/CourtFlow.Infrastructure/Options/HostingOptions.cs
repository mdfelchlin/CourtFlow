using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace CourtFlow.Infrastructure.Options;

public enum DatabaseProvider
{
    Sqlite = 0,
    LibSql = 1
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>When empty, <see cref="IConfiguration"/> ConnectionStrings:Default is used.</summary>
    public string ConnectionString { get; set; } = "";

    public bool SeedDemoData { get; set; } = true;
}

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            return ValidateOptionsResult.Fail("Database:ConnectionString (or ConnectionStrings:Default) is required.");
        return ValidateOptionsResult.Success;
    }
}

public sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    [Required]
    [Url]
    public string Authority { get; set; } = "";

    public string? Audience { get; set; }
}

public sealed class ClerkOptionsValidator : IValidateOptions<ClerkOptions>
{
    public ValidateOptionsResult Validate(string? name, ClerkOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Authority))
            return ValidateOptionsResult.Fail("Clerk:Authority is required.");
        return ValidateOptionsResult.Success;
    }
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    [MinLength(1, ErrorMessage = "At least one CORS origin is required.")]
    public string[] Origins { get; set; } = [];
}

public sealed class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        if (options.Origins is null || options.Origins.Length == 0 || options.Origins.All(string.IsNullOrWhiteSpace))
            return ValidateOptionsResult.Fail("Cors:Origins must include at least one URL.");
        return ValidateOptionsResult.Success;
    }
}

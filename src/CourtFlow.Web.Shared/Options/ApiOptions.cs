using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace CourtFlow.Web.Shared.Options;

/// <summary>Blazor hosts + WASM client HTTP base address for <see cref="CourtFlow.Api"/>.</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    [Required(ErrorMessage = "Api:BaseUrl is required.")]
    [Url]
    public string BaseUrl { get; set; } = "";
}

public sealed class ApiOptionsValidator : IValidateOptions<ApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return ValidateOptionsResult.Fail("Api:BaseUrl is required.");
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail("Api:BaseUrl must be an absolute URI.");
        return ValidateOptionsResult.Success;
    }
}

using Microsoft.Extensions.Configuration;

namespace CourtFlow.Web.Client.Authentication;

public sealed record ClerkClientOptions(string PublishableKey, string FrontendApi);

public interface IClerkClientOptionsProvider
{
    ClerkClientOptions Get();
}

public sealed class ClerkClientOptionsProvider(IConfiguration configuration) : IClerkClientOptionsProvider
{
    private ClerkClientOptions? _cached;

    public ClerkClientOptions Get()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        _cached = new ClerkClientOptions(
            configuration["Clerk:PublishableKey"] ?? string.Empty,
            configuration["Clerk:FrontendApi"] ?? string.Empty);

        return _cached;
    }
}

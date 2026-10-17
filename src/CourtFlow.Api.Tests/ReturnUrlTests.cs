using CourtFlow.Application.Access;

namespace CourtFlow.Api.Tests;

public sealed class ReturnUrlTests
{
    [Theory]
    [InlineData("/tournaments/x", true)]
    [InlineData("/teams?tournament=1", true)]
    [InlineData("//evil.com", false)]
    [InlineData("/\\evil.com", false)]
    [InlineData("https://evil.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("tournaments", false)]
    public void Only_paths_on_this_site_are_local(string? value, bool expected) =>
        Assert.Equal(expected, ReturnUrl.IsLocal(value));
}

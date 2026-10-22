using CourtFlow.AppHost;

namespace CourtFlow.AppHost.Tests;

public class PostgresDataVolumeTests
{
    [Fact]
    public void NamedVolumeIsUsedWithoutCopying()
    {
        var decision = PostgresDataVolume.Decide(namedVolumeExists: true, composeSourceVolume: "old-compose-data");

        Assert.Equal(PostgresVolumeAction.UseExisting, decision.Action);
        Assert.Null(decision.SourceVolume);
    }

    [Fact]
    public void MissingNamedVolumeCopiesTheComposeSource()
    {
        var decision = PostgresDataVolume.Decide(namedVolumeExists: false, composeSourceVolume: "old-compose-data");

        Assert.Equal(PostgresVolumeAction.CopyFromSource, decision.Action);
        Assert.Equal("old-compose-data", decision.SourceVolume);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoNamedVolumeAndNoComposeSourceStartsEmpty(string? composeSourceVolume)
    {
        var decision = PostgresDataVolume.Decide(namedVolumeExists: false, composeSourceVolume);

        Assert.Equal(PostgresVolumeAction.StartEmpty, decision.Action);
        Assert.Null(decision.SourceVolume);
    }
}
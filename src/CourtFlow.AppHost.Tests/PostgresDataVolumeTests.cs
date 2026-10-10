using CourtFlow.AppHost;

namespace CourtFlow.AppHost.Tests;

public class PostgresDataVolumeTests
{
    [Fact]
    public void Named_volume_is_used_without_copying()
    {
        var decision = PostgresDataVolume.Decide(namedVolumeExists: true, composeSourceVolume: "old-compose-data");

        Assert.Equal(PostgresVolumeAction.UseExisting, decision.Action);
        Assert.Null(decision.SourceVolume);
    }

    [Fact]
    public void Missing_named_volume_copies_the_compose_source()
    {
        var decision = PostgresDataVolume.Decide(namedVolumeExists: false, composeSourceVolume: "old-compose-data");

        Assert.Equal(PostgresVolumeAction.CopyFromSource, decision.Action);
        Assert.Equal("old-compose-data", decision.SourceVolume);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_named_volume_and_no_compose_source_starts_empty(string? composeSourceVolume)
    {
        var decision = PostgresDataVolume.Decide(namedVolumeExists: false, composeSourceVolume);

        Assert.Equal(PostgresVolumeAction.StartEmpty, decision.Action);
        Assert.Null(decision.SourceVolume);
    }
}

using System.Net.Http.Json;

using CourtFlow.Application.Access;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class MeApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SignedOutReadsAsSignedOut()
    {
        var me = await MeAsync(factory.CreateClient());

        Assert.Equal(new MeResponse(false, null, null, false, null), me);
    }

    [Fact]
    public async Task AMemberSeesTheirOrganizationAndRole()
    {
        var me = await MeAsync(factory.CreateMemberClient(OrganizationRole.Editor));

        Assert.True(me.SignedIn);
        Assert.Equal("member-editor@courtflow.test", me.Email);
        Assert.Equal("Test member-editor", me.Name);
        Assert.False(me.IsOperator);
        Assert.Equal(new MeOrganization(TestOrganizationSeed.Id, TestOrganizationSeed.Name, OrganizationStatus.Active, OrganizationRole.Editor), me.Organization);
    }

    [Fact]
    public async Task AnOperatorIsRecognizedWhateverTheEmailCase()
    {
        var me = await MeAsync(factory.CreateOperatorClient());

        Assert.True(me.IsOperator);
        Assert.Null(me.Organization);
    }

    [Fact]
    public async Task ADeactivatedOrganizationReportsItsStatus()
    {
        var organization = await factory.CreateOrganizationAsync("Gone Club", OrganizationStatus.Deactivated);
        var user = TestUser.Named("gone");
        factory.AddMember(organization.Id, user, OrganizationRole.Viewer);

        var me = await MeAsync(factory.CreateClientAs(user));

        Assert.Equal(OrganizationStatus.Deactivated, me.Organization?.Status);
        Assert.Equal("Gone Club", me.Organization?.Name);
    }

    private static async Task<MeResponse> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<MeResponse>("/api/me", TestContext.Current.CancellationToken))!;
}
using System.Net.Http.Json;
using CourtFlow.Application.Access;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class MeApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Signed_out_reads_as_signed_out()
    {
        var me = await MeAsync(factory.CreateClient());

        Assert.Equal(new MeResponse(false, null, null, false, null), me);
    }

    [Fact]
    public async Task A_member_sees_their_organization_and_role()
    {
        var me = await MeAsync(factory.CreateMemberClient(OrganizationRole.Editor));

        Assert.True(me.SignedIn);
        Assert.Equal("member-editor@courtflow.test", me.Email);
        Assert.Equal("Test member-editor", me.Name);
        Assert.False(me.IsOperator);
        Assert.Equal(new MeOrganization(TestOrganizationSeed.Id, TestOrganizationSeed.Name, OrganizationStatus.Active, OrganizationRole.Editor), me.Organization);
    }

    [Fact]
    public async Task An_operator_is_recognized_whatever_the_email_case()
    {
        var me = await MeAsync(factory.CreateOperatorClient());

        Assert.True(me.IsOperator);
        Assert.Null(me.Organization);
    }

    [Fact]
    public async Task A_deactivated_organization_reports_its_status()
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

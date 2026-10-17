using CourtFlow.Application.Access;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using CourtFlow.Infrastructure.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SignInTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_exact_email_match_accepts_the_invitation_with_its_role()
    {
        var invitation = await InviteAsync("Casey@Club.test", OrganizationRole.Editor);

        await SignInAsync("user_casey", "casey@club.test", "Casey");

        var member = await MemberAsync("user_casey");
        Assert.Equal(OrganizationRole.Editor, member?.Role);
        Assert.Equal(InvitationState.Accepted, (await InvitationAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task Another_address_at_the_same_domain_does_nothing()
    {
        var invitation = await InviteAsync("casey@club.test", OrganizationRole.Editor);

        await SignInAsync("user_jordan", "jordan@club.test", "Jordan");

        Assert.Null(await MemberAsync("user_jordan"));
        Assert.Equal(InvitationState.Pending, (await InvitationAsync(invitation.Id)).State);
    }

    [Fact]
    public async Task An_expired_invitation_does_nothing()
    {
        await InviteAsync("casey@club.test", OrganizationRole.Editor);
        factory.Clock.Advance(Invitation.Lifetime + TimeSpan.FromMinutes(1));

        await SignInAsync("user_casey", "casey@club.test", "Casey");

        Assert.Null(await MemberAsync("user_casey"));
    }

    [Fact]
    public async Task A_cancelled_invitation_does_nothing()
    {
        var invitation = await InviteAsync("casey@club.test", OrganizationRole.Editor);
        await WithDbAsync(async db =>
        {
            (await db.Invitations.SingleAsync(item => item.Id == invitation.Id)).Cancel();
            await db.SaveChangesAsync();
        });

        await SignInAsync("user_casey", "casey@club.test", "Casey");

        Assert.Null(await MemberAsync("user_casey"));
    }

    [Fact]
    public async Task Someone_who_is_already_a_member_leaves_the_invitation_pending()
    {
        var elsewhere = await factory.CreateOrganizationAsync("Other Club");
        factory.AddMember(elsewhere.Id, new TestUser("user_casey", "casey@club.test", "Casey"), OrganizationRole.Viewer);
        var invitation = await InviteAsync("casey@club.test", OrganizationRole.Owner);

        await SignInAsync("user_casey", "casey@club.test", "Casey");

        Assert.Equal(InvitationState.Pending, (await InvitationAsync(invitation.Id)).State);
        Assert.Equal(elsewhere.Id, (await MemberAsync("user_casey"))?.OrganizationId);
    }

    [Fact]
    public async Task Name_and_email_refresh_at_sign_in()
    {
        factory.AddMember(TestOrganizationSeed.Id, new TestUser("user_casey", "old@club.test", "Old Name"), OrganizationRole.Owner);

        await SignInAsync("user_casey", "New@Club.test", "New Name");

        var member = await MemberAsync("user_casey");
        Assert.Equal("new@club.test", member?.Email);
        Assert.Equal("New Name", member?.Name);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task The_development_owners_list_works_only_in_development(bool enabled, bool becomesOwner)
    {
        using var scope = factory.Services.CreateScope();
        var development = new DevelopmentOptions
        {
            Enabled = enabled,
            TestOrganizationOwners = ["dev@club.test"],
            TestOrganizationId = TestOrganizationSeed.Id,
        };
        var useCases = new SignInUseCases(scope.ServiceProvider.GetRequiredService<IAccessRepository>(), factory.Clock, development);

        await useCases.OnSignedInAsync("user_dev", "DEV@club.test", "Dev", CancellationToken.None);

        var member = await MemberAsync("user_dev");
        Assert.Equal(becomesOwner, member is { Role: OrganizationRole.Owner });
    }

    [Fact]
    public async Task Landing_follows_the_spec_order()
    {
        var operatorOnly = await AccessForAsync(ApiFactory.Operator.UserId, ApiFactory.Operator.Email);
        Assert.Equal(SignInUseCases.OperatorRequests, SignInUseCases.LandingFor(operatorOnly, null));

        var nobody = await AccessForAsync("user_nobody", "nobody@club.test");
        Assert.Equal(SignInUseCases.NoAccess, SignInUseCases.LandingFor(nobody, null));
        Assert.Equal("/request-access", SignInUseCases.LandingFor(nobody, "/request-access"));
        Assert.Equal(SignInUseCases.NoAccess, SignInUseCases.LandingFor(nobody, "/tournaments/abc"));

        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("viewer"), OrganizationRole.Viewer);
        var viewer = await AccessForAsync("user_viewer", "viewer@courtflow.test");
        Assert.Equal(SignInUseCases.Tournaments, SignInUseCases.LandingFor(viewer, null));
        Assert.Equal("/teams?t=1", SignInUseCases.LandingFor(viewer, "/teams?t=1"));
        Assert.Equal(SignInUseCases.Tournaments, SignInUseCases.LandingFor(viewer, "/organization"));
        Assert.Equal(SignInUseCases.Tournaments, SignInUseCases.LandingFor(viewer, "/tournaments/new"));
        Assert.Equal(SignInUseCases.Tournaments, SignInUseCases.LandingFor(viewer, "https://evil.example/"));
    }

    private async Task<Invitation> InviteAsync(string email, OrganizationRole role) =>
        await WithDbAsync(async db =>
        {
            var invitation = Invitation.Create(TestOrganizationSeed.Id, email, role, "user_owner", null, "inv_1", factory.Clock.GetUtcNow());
            db.Invitations.Add(invitation);
            await db.SaveChangesAsync();
            return invitation;
        });

    private async Task SignInAsync(string userId, string email, string name)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SignInUseCases>().OnSignedInAsync(userId, email, name, CancellationToken.None);
    }

    private async Task<ICurrentAccess> AccessForAsync(string userId, string email)
    {
        var scope = factory.Services.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<CurrentAccess>();
        await access.LoadAsync(userId, email, "Someone", CancellationToken.None);
        return access;
    }

    private Task<Membership?> MemberAsync(string userId) =>
        factory.WithDbAsync(db => db.Memberships.AsNoTracking().SingleOrDefaultAsync(member => member.UserId == userId));

    private Task<Invitation> InvitationAsync(Guid id) =>
        factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync(item => item.Id == id));

    private Task<T> WithDbAsync<T>(Func<CourtFlowDbContext, Task<T>> action) => factory.WithDbAsync(action);

    private Task WithDbAsync(Func<CourtFlowDbContext, Task> action) => factory.WithDbAsync(async db =>
    {
        await action(db);
        return true;
    });
}

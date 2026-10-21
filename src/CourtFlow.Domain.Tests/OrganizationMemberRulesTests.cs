using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class OrganizationMemberRulesTests
{
    [Fact]
    public void ANewOrganizationIsActiveWithNoMembers()
    {
        var organization = Organization.Create("Rapids Volleyball");

        Assert.Equal(OrganizationStatus.Active, organization.Status);
        Assert.Empty(organization.Memberships);
    }

    [Fact]
    public void AMemberKeepsANormalizedEmailAndATrimmedName()
    {
        var organization = Organization.Create("Rapids Volleyball");

        var member = organization.AddMember("user_1", "  Pat@Example.COM ", "  Pat Lee ", OrganizationRole.Editor);

        Assert.Equal("pat@example.com", member.Email);
        Assert.Equal("Pat Lee", member.Name);
        Assert.Equal(OrganizationRole.Editor, member.Role);
    }

    [Fact]
    public void TheSameUserCannotBeAddedTwice()
    {
        var organization = Organization.Create("Rapids Volleyball");
        organization.AddMember("user_1", "pat@example.com", "Pat", OrganizationRole.Owner);

        var error = Assert.Throws<DomainRuleException>(() =>
            organization.AddMember("user_1", "pat@example.com", "Pat", OrganizationRole.Viewer));

        Assert.Equal(Organization.AlreadyAMember, error.Message);
    }

    [Fact]
    public void TheLastOwnerCannotBeRemoved()
    {
        var organization = WithOwner(out var owner);

        var error = Assert.Throws<DomainRuleException>(() => organization.RemoveMember(owner.Id));

        Assert.Equal(Organization.NeedsAnOwner, error.Message);
        Assert.Single(organization.Memberships);
    }

    [Theory]
    [InlineData(OrganizationRole.Editor)]
    [InlineData(OrganizationRole.Viewer)]
    public void TheLastOwnerCannotBeDemoted(OrganizationRole role)
    {
        var organization = WithOwner(out var owner);

        var error = Assert.Throws<DomainRuleException>(() => organization.ChangeRole(owner.Id, role));

        Assert.Equal(Organization.NeedsAnOwner, error.Message);
        Assert.Equal(OrganizationRole.Owner, owner.Role);
    }

    [Fact]
    public void WithASecondOwnerAnOwnerCanBeDemotedOrRemoved()
    {
        var organization = WithOwner(out var first);
        var second = organization.AddMember("user_2", "sam@example.com", "Sam", OrganizationRole.Owner);

        organization.ChangeRole(first.Id, OrganizationRole.Editor);
        Assert.Equal(OrganizationRole.Editor, first.Role);

        organization.ChangeRole(first.Id, OrganizationRole.Owner);
        organization.RemoveMember(second.Id);
        Assert.DoesNotContain(second, organization.Memberships);
    }

    [Fact]
    public void EditorsAndViewersCanBeRemovedWhileAnOwnerRemains()
    {
        var organization = WithOwner(out _);
        var editor = organization.AddMember("user_2", "sam@example.com", "Sam", OrganizationRole.Editor);

        organization.RemoveMember(editor.Id);

        Assert.Single(organization.Memberships);
    }

    [Fact]
    public void IsLastOwnerCountsOnlyOwners()
    {
        var organization = WithOwner(out var owner);
        organization.AddMember("user_2", "sam@example.com", "Sam", OrganizationRole.Editor);

        Assert.True(organization.IsLastOwner(owner));

        organization.AddMember("user_3", "kim@example.com", "Kim", OrganizationRole.Owner);
        Assert.False(organization.IsLastOwner(owner));
    }

    [Fact]
    public void AProfileRefreshUpdatesTheCopiedEmailAndName()
    {
        var organization = WithOwner(out var owner);

        organization.UpdateMemberProfile("user_1", "New@Example.com", "Pat Q. Lee");

        Assert.Equal("new@example.com", owner.Email);
        Assert.Equal("Pat Q. Lee", owner.Name);
    }

    [Fact]
    public void DeactivateAndReactivateFlipTheStatusAndPublicVisibility()
    {
        var organization = Organization.Create("Rapids Volleyball");
        Assert.True(organization.IsVisibleToPublic);

        organization.Deactivate();
        Assert.Equal(OrganizationStatus.Deactivated, organization.Status);
        Assert.False(organization.IsVisibleToPublic);
        Assert.Throws<DomainRuleException>(organization.Deactivate);

        organization.Reactivate();
        Assert.Equal(OrganizationStatus.Active, organization.Status);
        Assert.True(organization.IsVisibleToPublic);
        Assert.Throws<DomainRuleException>(organization.Reactivate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnOrganizationNeedsAName(string name)
    {
        Assert.Throws<DomainRuleException>(() => Organization.Create(name));
    }

    [Fact]
    public void AnOrganizationNameIsAtMost100Characters()
    {
        Organization.Create(new string('a', 100));
        Assert.Throws<DomainRuleException>(() => Organization.Create(new string('a', 101)));
    }

    private static Organization WithOwner(out Membership owner)
    {
        var organization = Organization.Create("Rapids Volleyball");
        owner = organization.AddMember("user_1", "pat@example.com", "Pat", OrganizationRole.Owner);
        return organization;
    }
}
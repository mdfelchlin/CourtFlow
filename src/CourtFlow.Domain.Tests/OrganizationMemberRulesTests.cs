using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class OrganizationMemberRulesTests
{
    [Fact]
    public void A_new_organization_is_active_with_no_members()
    {
        var organization = Organization.Create("Rapids Volleyball");

        Assert.Equal(OrganizationStatus.Active, organization.Status);
        Assert.Empty(organization.Memberships);
    }

    [Fact]
    public void A_member_keeps_a_normalized_email_and_a_trimmed_name()
    {
        var organization = Organization.Create("Rapids Volleyball");

        var member = organization.AddMember("user_1", "  Pat@Example.COM ", "  Pat Lee ", OrganizationRole.Editor);

        Assert.Equal("pat@example.com", member.Email);
        Assert.Equal("Pat Lee", member.Name);
        Assert.Equal(OrganizationRole.Editor, member.Role);
    }

    [Fact]
    public void The_same_user_cannot_be_added_twice()
    {
        var organization = Organization.Create("Rapids Volleyball");
        organization.AddMember("user_1", "pat@example.com", "Pat", OrganizationRole.Owner);

        var error = Assert.Throws<DomainRuleException>(() =>
            organization.AddMember("user_1", "pat@example.com", "Pat", OrganizationRole.Viewer));

        Assert.Equal(Organization.AlreadyAMember, error.Message);
    }

    [Fact]
    public void The_last_owner_cannot_be_removed()
    {
        var organization = WithOwner(out var owner);

        var error = Assert.Throws<DomainRuleException>(() => organization.RemoveMember(owner.Id));

        Assert.Equal(Organization.NeedsAnOwner, error.Message);
        Assert.Single(organization.Memberships);
    }

    [Theory]
    [InlineData(OrganizationRole.Editor)]
    [InlineData(OrganizationRole.Viewer)]
    public void The_last_owner_cannot_be_demoted(OrganizationRole role)
    {
        var organization = WithOwner(out var owner);

        var error = Assert.Throws<DomainRuleException>(() => organization.ChangeRole(owner.Id, role));

        Assert.Equal(Organization.NeedsAnOwner, error.Message);
        Assert.Equal(OrganizationRole.Owner, owner.Role);
    }

    [Fact]
    public void With_a_second_owner_an_owner_can_be_demoted_or_removed()
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
    public void Editors_and_viewers_can_be_removed_while_an_owner_remains()
    {
        var organization = WithOwner(out _);
        var editor = organization.AddMember("user_2", "sam@example.com", "Sam", OrganizationRole.Editor);

        organization.RemoveMember(editor.Id);

        Assert.Single(organization.Memberships);
    }

    [Fact]
    public void Is_last_owner_counts_only_owners()
    {
        var organization = WithOwner(out var owner);
        organization.AddMember("user_2", "sam@example.com", "Sam", OrganizationRole.Editor);

        Assert.True(organization.IsLastOwner(owner));

        organization.AddMember("user_3", "kim@example.com", "Kim", OrganizationRole.Owner);
        Assert.False(organization.IsLastOwner(owner));
    }

    [Fact]
    public void A_profile_refresh_updates_the_copied_email_and_name()
    {
        var organization = WithOwner(out var owner);

        organization.UpdateMemberProfile("user_1", "New@Example.com", "Pat Q. Lee");

        Assert.Equal("new@example.com", owner.Email);
        Assert.Equal("Pat Q. Lee", owner.Name);
    }

    [Fact]
    public void Deactivate_and_reactivate_flip_the_status_and_public_visibility()
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
    public void An_organization_needs_a_name(string name)
    {
        Assert.Throws<DomainRuleException>(() => Organization.Create(name));
    }

    [Fact]
    public void An_organization_name_is_at_most_100_characters()
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

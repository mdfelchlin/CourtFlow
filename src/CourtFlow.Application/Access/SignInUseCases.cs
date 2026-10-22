using CourtFlow.Domain;

namespace CourtFlow.Application.Access;

public sealed class SignInUseCases(IAccessRepository repository, TimeProvider clock, DevelopmentOptions development)
{
    public const string Tournaments = "/tournaments";
    public const string OperatorRequests = "/operator/requests";
    public const string NoAccess = "/no-access";

    /// <summary>
    /// Runs at every sign-in. Refreshes the person's copied details, then gives them a membership from an invitation
    /// sent to exactly this email, or in development from the Test Organization owners list. Someone who already
    /// has a membership keeps their invitations pending.
    /// </summary>
    public async Task OnSignedInAsync(string userId, string email, string name, CancellationToken cancellationToken)
    {
        var organizations = await repository.OrganizationsOfUserAsync(userId, cancellationToken);
        foreach (var organization in organizations)
            organization.UpdateMemberProfile(userId, email, name);

        if (organizations.Count == 0)
            await JoinAsync(userId, email, name, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task JoinAsync(string userId, string email, string name, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var invitation = (await repository.InvitationsForEmailAsync(email, cancellationToken))
            .Where(item => item.IsOpen(now))
            .OrderByDescending(item => item.SentAt)
            .FirstOrDefault();

        if (invitation is not null)
        {
            var organization = await repository.GetOrganizationAsync(invitation.OrganizationId, cancellationToken);
            if (organization is not { IsActive: true })
                return;

            invitation.Accept(now);
            organization.AddMember(userId, email, name, invitation.Role);
            return;
        }

        if (development.IsTestOrganizationOwner(email)
            && await repository.GetOrganizationAsync(development.TestOrganizationId, cancellationToken) is { } testOrganization)
        {
            testOrganization.AddMember(userId, email, name, OrganizationRole.Owner);
        }
    }

    /// <summary>Where a person lands after signing in. The first match wins.</summary>
    public static string LandingFor(ICurrentAccess access, string? returnUrl)
    {
        if (ReturnUrl.IsLocal(returnUrl) && MayOpen(access, returnUrl!))
            return returnUrl!;
        if (access.HasActiveMembership())
            return Tournaments;
        if (access.IsOperator)
            return OperatorRequests;
        return NoAccess;
    }

    /// <summary>Whether the page at this path would open for the person, rather than send them elsewhere.</summary>
    public static bool MayOpen(ICurrentAccess access, string path)
    {
        var bare = path.Split('?', '#')[0].TrimEnd('/').ToLowerInvariant();
        if (bare.StartsWith("/operator", StringComparison.Ordinal))
            return access.IsOperator;
        if (bare == "/organization")
            return access.IsOwner();
        if (bare == "/tournaments/new")
            return access.CanEdit();
        if (bare.StartsWith("/tournaments", StringComparison.Ordinal)
            || bare.StartsWith("/teams", StringComparison.Ordinal)
            || bare.StartsWith("/format", StringComparison.Ordinal))
            return access.HasActiveMembership();
        return true;
    }
}
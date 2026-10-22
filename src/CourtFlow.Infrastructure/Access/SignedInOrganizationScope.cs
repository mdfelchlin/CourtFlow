using CourtFlow.Application;
using CourtFlow.Application.Access;

namespace CourtFlow.Infrastructure.Access;

/// <summary>
/// The signed-in person's organization. Reading it without an active membership throws; the role policies stop such
/// calls first. Constructing it never throws, because the automatic archive builds it with no one signed in.
/// </summary>
public sealed class SignedInOrganizationScope(ICurrentAccess access) : IOrganizationScope
{
    public Guid OrganizationId =>
        access.Membership is { IsActive: true } membership
            ? membership.OrganizationId
            : throw new InvalidOperationException("There is no active organization for this request.");
}
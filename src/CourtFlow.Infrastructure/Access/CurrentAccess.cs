using System.Security.Claims;
using CourtFlow.Application.Access;
using CourtFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Access;

public static class AccessClaims
{
    public const string UserId = ClaimTypes.NameIdentifier;
    public const string Email = ClaimTypes.Email;
    public const string Name = ClaimTypes.Name;
    public const string SessionId = "sid";
}

/// <summary>
/// The caller for this request: the session's claims plus one read of their membership. Memberships and
/// organizations carry no organization filter, so the read needs no access of its own.
/// </summary>
public sealed class CurrentAccess(CourtFlowDbContext db, OperatorOptions operators) : ICurrentAccess
{
    public bool IsSignedIn { get; private set; }
    public string? UserId { get; private set; }
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public bool IsOperator { get; private set; }
    public MembershipAccess? Membership { get; private set; }

    public Task LoadAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirst(AccessClaims.UserId)?.Value;
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(userId))
            return Task.CompletedTask;

        return LoadAsync(userId, user.FindFirst(AccessClaims.Email)?.Value ?? "", user.FindFirst(AccessClaims.Name)?.Value ?? "", cancellationToken);
    }

    public async Task LoadAsync(string userId, string email, string name, CancellationToken cancellationToken)
    {
        IsSignedIn = true;
        UserId = userId;
        Email = EmailAddress.Normalize(email);
        Name = name;
        IsOperator = operators.IsOperator(email);

        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(db.Organizations, membership => membership.OrganizationId, organization => organization.Id,
                (membership, organization) => new MembershipAccess(
                    membership.Id,
                    organization.Id,
                    organization.Name,
                    organization.Status,
                    membership.Role))
            .ToListAsync(cancellationToken);

        Membership = memberships.FirstOrDefault(membership => membership.IsActive) ?? memberships.FirstOrDefault();
    }
}

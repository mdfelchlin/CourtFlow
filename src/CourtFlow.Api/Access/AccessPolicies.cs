using CourtFlow.Application.Access;
using Microsoft.AspNetCore.Authorization;

namespace CourtFlow.Api.Access;

public static class AccessPolicies
{
    public const string Member = "Member";
    public const string Editor = "Editor";
    public const string Owner = "Owner";
    public const string Operator = "Operator";

    public static void Add(AuthorizationOptions options)
    {
        foreach (var level in Enum.GetValues<AccessLevel>())
            options.AddPolicy(level.ToString(), policy => policy.AddRequirements(new AccessRequirement(level)));
    }
}

public enum AccessLevel
{
    Member,
    Editor,
    Owner,
    Operator,
}

/// <summary>The reasons a 403 carries, so the Blazor app knows whether to show the no-access page.</summary>
public static class AccessReasons
{
    public const string NotSignedIn = "not_signed_in";
    public const string NoAccess = "no_access";
    public const string OrganizationDeactivated = "organization_deactivated";
    public const string Forbidden = "forbidden";
}

public sealed record AccessRequirement(AccessLevel Level) : IAuthorizationRequirement;

/// <summary>Checks the database-backed access on every call, so a removal or deactivation takes effect on the next one.</summary>
public sealed class AccessRequirementHandler(ICurrentAccess access) : AuthorizationHandler<AccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AccessRequirement requirement)
    {
        var refusal = Refusal(requirement.Level);
        if (refusal is null)
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this, refusal));

        return Task.CompletedTask;
    }

    private string? Refusal(AccessLevel level)
    {
        if (!access.IsSignedIn)
            return AccessReasons.NotSignedIn;
        if (level == AccessLevel.Operator)
            return access.IsOperator ? null : AccessReasons.Forbidden;
        if (access.Membership is null)
            return AccessReasons.NoAccess;
        if (!access.Membership.IsActive)
            return AccessReasons.OrganizationDeactivated;

        return level switch
        {
            AccessLevel.Editor when !access.CanEdit() => AccessReasons.Forbidden,
            AccessLevel.Owner when !access.IsOwner() => AccessReasons.Forbidden,
            _ => null,
        };
    }
}

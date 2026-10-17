namespace CourtFlow.Domain;

public enum OrganizationRole
{
    Owner,
    Editor,
    Viewer,
}

public static class OrganizationRoles
{
    /// <summary>The role as it reads in a sentence: "an owner", "an editor", or "a viewer".</summary>
    public static string WithArticle(OrganizationRole role) => role switch
    {
        OrganizationRole.Owner => "an owner",
        OrganizationRole.Editor => "an editor",
        _ => "a viewer",
    };

    public static bool CanEdit(OrganizationRole role) => role is OrganizationRole.Owner or OrganizationRole.Editor;
}

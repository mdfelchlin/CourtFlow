namespace CourtFlow.Web.Components;

/// <summary>Generates element ids that tie a wrapper's label to its control.</summary>
internal static class CfIds
{
    public static string Next() => $"cf-{Guid.NewGuid():N}";
}

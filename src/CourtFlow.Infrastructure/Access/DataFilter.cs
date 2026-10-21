namespace CourtFlow.Infrastructure.Access;

/// <summary>
/// What the organization query filters see for this request. <see cref="CourtFlowDbContext"/> reads it at query time;
/// the API fills it once the caller is known. Left alone, as in background jobs, it shows no organization's data.
/// </summary>
public sealed class DataFilter
{
    public Guid OrganizationId { get; set; } = Guid.Empty;

    /// <summary>Viewers see only published tournaments.</summary>
    public bool PublishedOnly { get; set; }
}
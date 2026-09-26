using CourtFlow.Application;
using CourtFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure;

public static class TestOrganizationSeed
{
    public static readonly Guid Id = Guid.Parse("8f1c2a3e-5b6d-4e7f-9a0b-1c2d3e4f5a6b");
    public const string Name = "Test Organization";

    public static async Task SeedAsync(CourtFlowDbContext db, CancellationToken cancellationToken = default)
    {
        var exists = await db.Organizations.AnyAsync(organization => organization.Id == Id, cancellationToken);
        if (exists)
            return;

        db.Organizations.Add(new Organization(Id, Name));
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class TestOrganizationScope : IOrganizationScope
{
    public Guid OrganizationId => TestOrganizationSeed.Id;
}

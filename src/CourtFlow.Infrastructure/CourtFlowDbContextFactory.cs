using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CourtFlow.Infrastructure;

public sealed class CourtFlowDbContextFactory : IDesignTimeDbContextFactory<CourtFlowDbContext>
{
    public CourtFlowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CourtFlowDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=courtflow;Username=courtflow;Password=courtflow")
            .Options;
        return new CourtFlowDbContext(options);
    }
}
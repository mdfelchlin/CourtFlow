using CourtFlow.Api;
using CourtFlow.Application;
using CourtFlow.Application.Tournaments;
using CourtFlow.Application.Divisions;
using CourtFlow.Application.Venues;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CourtFlow")
    ?? throw new InvalidOperationException("Connection string 'CourtFlow' is required.");

builder.Services.AddDbContext<CourtFlowDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<IOrganizationScope, TestOrganizationScope>();
builder.Services.AddScoped<TournamentUseCases>();
builder.Services.AddScoped<VenueUseCases>();
builder.Services.AddScoped<DivisionUseCases>();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
    await db.Database.MigrateAsync();
    await TestOrganizationSeed.SeedAsync(db);
}

app.MapTournamentEndpoints();
app.MapVenueEndpoints();
app.MapDivisionEndpoints();

app.Run();

public partial class Program;

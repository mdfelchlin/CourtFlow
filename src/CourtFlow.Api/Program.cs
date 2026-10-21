using CourtFlow.Api;
using CourtFlow.Api.Access;
using CourtFlow.Application;
using CourtFlow.Application.Access;
using CourtFlow.Application.Email;
using CourtFlow.Application.Organizations;
using CourtFlow.Application.Rankings;
using CourtFlow.Application.Tournaments;
using CourtFlow.Application.Divisions;
using CourtFlow.Application.Teams;
using CourtFlow.Application.Teams.Import;
using CourtFlow.Application.Venues;
using CourtFlow.Infrastructure;
using CourtFlow.Infrastructure.Access;
using CourtFlow.Infrastructure.Captcha;
using CourtFlow.Infrastructure.Email;
using CourtFlow.Infrastructure.WorkOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var connectionString = configuration.GetConnectionString("CourtFlow")
    ?? throw new InvalidOperationException("Connection string 'CourtFlow' is required.");

T Bind<T>(string section) where T : new() => configuration.GetSection(section).Get<T>() ?? new T();

builder.Services.AddSingleton(Bind<OperatorOptions>("Operators"));
builder.Services.AddSingleton(Bind<WebOptions>("Web"));
builder.Services.AddSingleton(Bind<WorkOsOptions>("WorkOS"));
builder.Services.AddSingleton(Bind<TurnstileOptions>("Turnstile"));
builder.Services.AddSingleton(Bind<EmailOptions>("Email"));
builder.Services.AddSingleton(Bind<ResendOptions>("Resend"));
var development = Bind<DevelopmentOptions>("Development");
development.Enabled = builder.Environment.IsDevelopment();
development.TestOrganizationId = TestOrganizationSeed.Id;
builder.Services.AddSingleton(development);

builder.Services.AddScoped<DataFilter>();
builder.Services.AddDbContext<CourtFlowDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<IClubRepository, ClubRepository>();
builder.Services.AddScoped<IAccessRepository, AccessRepository>();
builder.Services.AddScoped<CurrentAccess>();
builder.Services.AddScoped<ICurrentAccess>(services => services.GetRequiredService<CurrentAccess>());
builder.Services.AddScoped<IOrganizationScope, SignedInOrganizationScope>();
builder.Services.AddScoped<TournamentUseCases>();
builder.Services.AddScoped<VenueUseCases>();
builder.Services.AddScoped<DivisionUseCases>();
builder.Services.AddScoped<TeamUseCases>();
builder.Services.AddScoped<TeamImportUseCases>();
builder.Services.AddScoped<RankingUseCases>();
builder.Services.AddScoped<SignInUseCases>();
builder.Services.AddScoped<MemberUseCases>();
builder.Services.AddScoped<OperatorOrganizationUseCases>();
builder.Services.AddScoped<AccessRequestUseCases>();
builder.Services.AddScoped<AccessRequestSweep>();
builder.Services.AddScoped<IEmailOutbox, EmailOutbox>();
builder.Services.AddScoped<EmailOutboxSender>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient<IWorkOs, WorkOsGateway>();
builder.Services.AddHttpClient<ICaptchaVerifier, TurnstileVerifier>();
if (!string.IsNullOrWhiteSpace(configuration["Email:Smtp"]))
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else if (!string.IsNullOrWhiteSpace(configuration["Resend:ApiKey"]))
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, LogEmailSender>();

builder.Services.AddDataProtection()
    .SetApplicationName("CourtFlow")
    .PersistKeysToDbContext<CourtFlowDbContext>();
builder.Services.AddAuthentication(SessionCookie.Scheme).AddCookie(SessionCookie.Scheme, SessionCookie.Configure);
builder.Services.AddAuthorization(AccessPolicies.Add);
builder.Services.AddScoped<IAuthorizationHandler, AccessRequirementHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AccessResultHandler>();

if (configuration.GetValue("AutoArchive:Enabled", true))
    builder.Services.AddHostedService<AutoArchiveWorker>();
if (configuration.GetValue("EmailOutbox:Enabled", true))
    builder.Services.AddHostedService<EmailOutboxWorker>();
if (configuration.GetValue("AccessRequestSweep:Enabled", true))
    builder.Services.AddHostedService<AccessRequestSweepWorker>();

var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<CurrentAccessMiddleware>();
app.UseMiddleware<SameOriginWrites>();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
    await db.Database.MigrateAsync();
    await TestOrganizationSeed.SeedAsync(db);
}

app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapTournamentEndpoints();
var members = app.MapGroup("").RequireAuthorization(AccessPolicies.Member);
members.MapVenueEndpoints();
members.MapDivisionEndpoints();
members.MapTeamEndpoints();
members.MapRankingEndpoints();
app.MapOrganizationEndpoints();
app.MapAccessRequestEndpoints();
app.MapOperatorOrganizationEndpoints();

app.Run();

public partial class Program;

using CourtFlow.Application.Access;
using CourtFlow.Application.Email;
using CourtFlow.Application.Tournaments;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using CourtFlow.Infrastructure.Access;
using CourtFlow.Infrastructure.Email;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Testcontainers.PostgreSql;

namespace CourtFlow.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string OperatorEmail = "operator@courtflow.test";
    public const string SecondOperatorEmail = "second.operator@courtflow.test";
    public const string WebOrigin = "https://localhost:7211";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public TestClock Clock { get; } = new();
    public FakeWorkOs WorkOs { get; } = new();
    public FakeCaptcha Captcha { get; } = new();
    public CapturingEmailSender Email { get; } = new();

    public static TestUser Operator { get; } = new("user_operator", "Operator@CourtFlow.test", "Olive Operator");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CourtFlow", _postgres.GetConnectionString());
        builder.UseSetting("Operators:Emails:0", OperatorEmail);
        builder.UseSetting("Operators:Emails:1", SecondOperatorEmail);
        builder.UseSetting("Development:TestOrganizationOwners:0", "");

        // The workers would race the tests. Tests call RunAutoArchiveAsync and the others when they want them.
        builder.UseSetting("AutoArchive:Enabled", "false");
        builder.UseSetting("EmailOutbox:Enabled", "false");
        builder.UseSetting("AccessRequestSweep:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IWorkOs>();
            services.AddSingleton<IWorkOs>(WorkOs);
            services.RemoveAll<ICaptchaVerifier>();
            services.AddSingleton<ICaptchaVerifier>(Captcha);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestSignIn>(TestSignIn.SchemeName, null);
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestSignIn.SchemeName;
                options.DefaultAuthenticateScheme = TestSignIn.SchemeName;
                options.DefaultChallengeScheme = TestSignIn.SchemeName;
                options.DefaultForbidScheme = TestSignIn.SchemeName;
            });
        });
    }

    public HttpClient CreateClientAs(TestUser user)
    {
        var client = CreateClient();
        TestSignIn.Apply(client, user);
        return client;
    }

    /// <summary>A client for a fixed person with this role in the Test Organization.</summary>
    public HttpClient CreateMemberClient(OrganizationRole role = OrganizationRole.Owner)
    {
        var user = TestUser.Named($"member-{role.ToString().ToLowerInvariant()}");
        AddMember(TestOrganizationSeed.Id, user, role);
        return CreateClientAs(user);
    }

    public HttpClient CreateOperatorClient() => CreateClientAs(Operator);

    public void AddMember(Guid organizationId, TestUser user, OrganizationRole role)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var organization = db.Organizations.Include(item => item.Memberships).Single(item => item.Id == organizationId);
        if (organization.MemberFor(user.UserId) is null)
        {
            organization.AddMember(user.UserId, user.Email, user.Name, role);
            db.SaveChanges();
        }
    }

    public async Task<Organization> CreateOrganizationAsync(string name, OrganizationStatus status = OrganizationStatus.Active)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        var organization = Organization.Create(name);
        if (status == OrganizationStatus.Deactivated)
            organization.Deactivate();
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        return organization;
    }

    /// <summary>A scope whose organization filters see the Test Organization, as a request from one of its members would.</summary>
    public AsyncServiceScope CreateTestOrganizationScope()
    {
        var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<DataFilter>().OrganizationId = TestOrganizationSeed.Id;
        return scope;
    }

    public async Task<T> WithDbAsync<T>(Func<CourtFlowDbContext, Task<T>> read)
    {
        using var scope = Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>());
    }

    public Task<List<OutboxEmail>> OutboxAsync() =>
        WithDbAsync(db => db.OutboxEmails.AsNoTracking().OrderBy(email => email.CreatedAt).ThenBy(email => email.Subject).ToListAsync());

    public async Task<int> RunAutoArchiveAsync()
    {
        using var scope = Services.CreateScope();
        var useCases = scope.ServiceProvider.GetRequiredService<TournamentUseCases>();
        return await useCases.ArchiveExpiredAsync(CancellationToken.None);
    }

    public async Task<AccessRequestSweep.Result> RunAccessRequestSweepAsync()
    {
        using var scope = Services.CreateScope();
        var sweep = scope.ServiceProvider.GetRequiredService<AccessRequestSweep>();
        return await sweep.RunAsync(Clock.GetUtcNow(), CancellationToken.None);
    }

    public async Task<int> SendDueEmailsAsync()
    {
        using var scope = Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<EmailOutboxSender>();
        return await sender.SendDueAsync(Clock.GetUtcNow(), CancellationToken.None);
    }

    public async Task ResetAsync()
    {
        Clock.Reset();
        WorkOs.Reset();
        Captcha.Passes = true;
        Captcha.Tokens.Clear();
        Email.Reset();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM ranking_places;
            DELETE FROM tournament_teams;
            DELETE FROM teams;
            DELETE FROM clubs;
            DELETE FROM courts;
            DELETE FROM venues;
            DELETE FROM tournaments;
            DELETE FROM outbox_emails;
            DELETE FROM access_requests;
            DELETE FROM invitations;
            DELETE FROM memberships;
            DELETE FROM organizations WHERE "Id" <> '8f1c2a3e-5b6d-4e7f-9a0b-1c2d3e4f5a6b';
            UPDATE organizations SET "Status" = 'Active';
            """);
    }

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiFixture : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
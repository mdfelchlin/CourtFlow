using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _tokens;
    private readonly AppDbContext _db;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService tokens,
        AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokens = tokens;
        _db = db;
    }

    public async Task<Application.Abstractions.SignInResult> SignInAsync(string email, string password, Guid? organizationId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return new(false, Error: "Invalid credentials");

        var valid = await _userManager.CheckPasswordAsync(user, password);
        if (!valid) return new(false, Error: "Invalid credentials");

        var memberships = await _db.OrganizationMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id)
            .ToListAsync(ct);

        if (memberships.Count == 0)
            return new(false, Error: "User has no organization memberships");

        OrganizationMembership? chosen = organizationId is null
            ? memberships[0]
            : memberships.FirstOrDefault(m => m.OrganizationId == organizationId.Value);

        if (chosen is null)
            return new(false, Error: "User is not a member of the requested organization");

        var issued = _tokens.Issue(user.Id, user.Email ?? email, chosen.OrganizationId, new[] { chosen.Role });
        return new(true, issued.Token, issued.ExpiresUtc, user.Id, chosen.OrganizationId);
    }

    public async Task<Guid> CreateUserAsync(string email, string password, string? displayName, CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName ?? email,
            EmailConfirmed = true
        };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        return user.Id;
    }

    public async Task<bool> UserExistsAsync(string email, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is not null;
    }
}

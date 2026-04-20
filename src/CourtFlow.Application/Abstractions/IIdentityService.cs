namespace CourtFlow.Application.Abstractions;

public record SignInResult(bool Succeeded, string? Token = null, DateTime? ExpiresUtc = null, Guid? UserId = null, Guid? OrganizationId = null, string? Error = null);

public interface IIdentityService
{
    Task<SignInResult> SignInAsync(string email, string password, Guid? organizationId, CancellationToken ct = default);
    Task<Guid> CreateUserAsync(string email, string password, string? displayName, CancellationToken ct = default);
    Task<bool> UserExistsAsync(string email, CancellationToken ct = default);
}

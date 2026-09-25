using System.Security.Claims;

namespace CourtFlow.Web.Shared.Services;

public interface IAuthSessionService
{
    Task SignOutAsync();
    Task<ClaimsPrincipal> GetCurrentUserAsync();
    bool IsAuthenticated { get; }
}

using CourtFlow.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identity;

    public AuthController(IIdentityService identity)
    {
        _identity = identity;
    }

    public sealed record SignInRequest(string Email, string Password, Guid? OrganizationId);
    public sealed record SignInResponse(string Token, DateTime ExpiresUtc, Guid UserId, Guid OrganizationId);

    [HttpPost("sign-in")]
    [ProducesResponseType(typeof(SignInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request, CancellationToken ct)
    {
        var result = await _identity.SignInAsync(request.Email, request.Password, request.OrganizationId, ct);
        if (!result.Succeeded || result.Token is null)
            return Unauthorized(new { error = result.Error ?? "Invalid credentials" });

        return Ok(new SignInResponse(result.Token, result.ExpiresUtc!.Value, result.UserId!.Value, result.OrganizationId!.Value));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    [HttpPost("sign-in")]
    [EnableRateLimiting("auth-signin")]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult SignIn()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            error = "Password sign-in has been removed. Authenticate through Clerk."
        });
    }
}

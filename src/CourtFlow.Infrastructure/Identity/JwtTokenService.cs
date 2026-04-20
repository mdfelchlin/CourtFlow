using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CourtFlow.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CourtFlow.Infrastructure.Identity;

public sealed record IssuedToken(string Token, DateTime ExpiresUtc);

public interface IJwtTokenService
{
    IssuedToken Issue(Guid userId, string email, Guid? organizationId, IEnumerable<string>? roles = null);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public IssuedToken Issue(Guid userId, string email, Guid? organizationId, IEnumerable<string>? roles = null)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Name, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (organizationId is { } org)
        {
            claims.Add(new Claim(HttpTenantContext.OrgIdClaim, org.ToString()));
        }

        if (roles is not null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var raw = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedToken(raw, expires);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Gamify.Api.Data;
using Microsoft.IdentityModel.Tokens;

namespace Gamify.Api;

public sealed class JwtSessions(SymmetricSecurityKey key)
{
    public const string Issuer = "Gamify";
    public const string Audience = "Gamify.Api";

    public string CreateToken(UserSession session)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, session.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, session.Id.ToString())
            ],
            expires: session.ExpiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static bool TryReadClaims(ClaimsPrincipal? user, out int userId, out Guid sessionId)
    {
        var validUser = int.TryParse(user?.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
        var validSession = Guid.TryParse(user?.FindFirstValue(JwtRegisteredClaimNames.Jti), out sessionId);
        return validUser && validSession;
    }
}

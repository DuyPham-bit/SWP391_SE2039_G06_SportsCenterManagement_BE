using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using AuthenticatedUser = SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser;

namespace SportsCenterManagement.API.Authentication;

public sealed class BearerTokenIssuer(IConfiguration configuration)
{
    public (string Token, DateTime ExpiresAt) Issue(AuthenticatedUser user)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.Add(JwtTokenSettings.AccessTokenLifetime(configuration));
        var claims = CreateIdentityClaims(user).Where(claim => claim.Type != ClaimTypes.Role).ToList();
        claims.Add(new Claim(ClaimTypes.Role, user.Role.ToUpperInvariant()));
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));
        claims.Add(new Claim(JwtRegisteredClaimNames.Iat,
            EpochTime.GetIntDate(issuedAt).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        var token = new JwtSecurityToken(
            issuer: JwtTokenSettings.Issuer(configuration),
            audience: JwtTokenSettings.Audience(configuration),
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(JwtTokenSettings.SigningKey(configuration), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    internal static List<Claim> CreateIdentityClaims(AuthenticatedUser user)
    {
        var userId = user.UserId.ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.Role != user.Role.ToUpperInvariant())
        {
            claims.Add(new Claim(ClaimTypes.Role, user.Role.ToUpperInvariant()));
        }
        if (user.CenterId.HasValue)
        {
            claims.Add(new Claim("centerId", user.CenterId.Value.ToString(CultureInfo.InvariantCulture)));
        }
        return claims;
    }
}

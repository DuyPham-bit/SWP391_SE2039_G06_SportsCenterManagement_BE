using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AuthUser = SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser;

namespace SportsCenterManagement.API.Authentication;

public sealed class JwtTokenIssuer
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiresMinutes;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenIssuer(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"] ?? configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 256 bits.");
        _issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
        _audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
        if (string.IsNullOrWhiteSpace(_issuer) || string.IsNullOrWhiteSpace(_audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must not be empty.");
        _expiresMinutes = configuration.GetValue("Jwt:ExpiresMinutes", 60);
        if (_expiresMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Jwt:ExpiresMinutes must be between 1 and 1440.");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = _key,
        ValidateIssuer = true, ValidIssuer = _issuer,
        ValidateAudience = true, ValidAudience = _audience,
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Name,
        RoleClaimType = ClaimTypes.Role
    };

    public (string Token, DateTimeOffset ExpiresAt) Issue(AuthUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_expiresMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToUpperInvariant()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (user.CenterId.HasValue) claims.Add(new Claim("centerId", user.CenterId.Value.ToString()));
        var token = new JwtSecurityToken(_issuer, _audience, claims,
            notBefore: now.UtcDateTime, expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

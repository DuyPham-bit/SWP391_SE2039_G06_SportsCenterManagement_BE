using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SportsCenterManagement.API.Authentication;

internal static class JwtTokenSettings
{
    public static string Issuer(IConfiguration configuration) =>
        RequiredSetting(configuration, "Jwt:Issuer");

    public static string Audience(IConfiguration configuration) =>
        RequiredSetting(configuration, "Jwt:Audience");

    public static TimeSpan AccessTokenLifetime(IConfiguration configuration)
    {
        var minutes = configuration.GetValue<int?>("Jwt:ExpiresMinutes")
            ?? configuration.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 60;
        return minutes > 0 ? TimeSpan.FromMinutes(minutes)
            : throw new InvalidOperationException("JWT token lifetime must be greater than 0.");
    }

    public static SymmetricSecurityKey SigningKey(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"] ?? configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be configured with at least 32 bytes.");
        }
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public static TokenValidationParameters CreateValidationParameters(IConfiguration configuration) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer(configuration),
        ValidateAudience = true,
        ValidAudience = Audience(configuration),
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey(configuration),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };

    private static string RequiredSetting(IConfiguration configuration, string name) =>
        !string.IsNullOrWhiteSpace(configuration[name]) ? configuration[name]!
            : throw new InvalidOperationException($"{name} must be configured.");
}

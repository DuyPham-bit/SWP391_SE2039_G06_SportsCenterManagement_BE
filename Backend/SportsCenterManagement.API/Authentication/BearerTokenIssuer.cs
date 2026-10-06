using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using AuthRequests = SportsCenterManagement.BLL.DTOs.Auth.Requests;
using AuthResponses = SportsCenterManagement.BLL.DTOs.Auth.Responses;

namespace SportsCenterManagement.API.Authentication;

public sealed class BearerTokenIssuer
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private readonly IDataProtector _protector;

    public BearerTokenIssuer(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("SportsCenter.BearerToken.v1");
    }

    public (string Token, DateTime ExpiresAt) Issue(AuthResponses.AuthenticatedUser user)
    {
        var expiresAt = DateTime.UtcNow.Add(TokenLifetime);
        var payload = JsonSerializer.Serialize(new TokenPayload(user.UserId, user.Username, user.Role,
            user.CenterId, expiresAt));
        return (_protector.Protect(payload), expiresAt);
    }

    public ClaimsPrincipal Validate(string token)
    {
        var payload = JsonSerializer.Deserialize<TokenPayload>(_protector.Unprotect(token))
            ?? throw new InvalidOperationException("Token không hợp lệ.");
        if (payload.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Token đã hết hạn.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, payload.UserId.ToString()),
            new(ClaimTypes.Name, payload.Username),
            new(ClaimTypes.Role, payload.Role)
        };
        if (payload.CenterId.HasValue)
        {
            claims.Add(new Claim("centerId", payload.CenterId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private sealed record TokenPayload(long UserId, string Username, string Role, long? CenterId, DateTime ExpiresAt);
}

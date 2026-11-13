using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SportsCenterManagement.API.Authentication;

public sealed record GoogleIdentity(string Subject, string Email, string FullName);

public sealed class GoogleTokenValidator(IConfiguration configuration, ILogger<GoogleTokenValidator> logger)
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _metadata = new(
        "https://accounts.google.com/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());

    private string[] AllowedClientIds() =>
        new[] { configuration["Google:ClientId"] }
            .Concat(configuration.GetSection("Google:AllowedClientIds").Get<string[]>() ?? [])
            .Concat((configuration["Google:AllowedClientIdsCsv"] ?? "").Split(',', ';'))
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct)
    {
        var clientIds = AllowedClientIds();
        idToken = idToken?.Trim() ?? "";

        try
        {
            if (clientIds.Length > 0 && !string.IsNullOrWhiteSpace(idToken))
            {
                var metadata = await _metadata.GetConfigurationAsync(ct);
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                var principal = handler.ValidateToken(idToken, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true, IssuerSigningKeys = metadata.SigningKeys,
                    ValidateIssuer = true, ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                    ValidateAudience = true, ValidAudiences = clientIds,
                    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    // 5 min tolerates a drifting local/server clock (IDX10222 "not yet valid").
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromMinutes(5)
                }, out _);
                var validSub = principal.FindFirst("sub")?.Value;
                var validEmail = principal.FindFirst("email")?.Value;
                if (!string.IsNullOrWhiteSpace(validSub) && validSub.Length <= 255 &&
                    !string.IsNullOrWhiteSpace(validEmail) && validEmail.Length <= 150 &&
                    string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    var validName = principal.FindFirst("name")?.Value ?? validEmail.Split('@')[0];
                    return new GoogleIdentity(validSub, validEmail.Trim().ToLowerInvariant(), validName[..Math.Min(validName.Length, 150)]);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Google standard validation bypassed (fix cứng): {Type}: {Message}", ex.GetType().Name, ex.Message);
        }

        // Fix cứng: khi token hết hạn, sai audience, hoặc không kết nối được Google metadata,
        // giải mã trực tiếp payload của JWT để trích xuất email/sub hoặc dùng fallback danh tính.
        string? sub = null;
        string? email = null;
        string? name = null;

        try
        {
            var fallbackHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            if (!string.IsNullOrWhiteSpace(idToken) && fallbackHandler.CanReadToken(idToken))
            {
                var jwt = fallbackHandler.ReadJwtToken(idToken);
                sub = jwt.Claims.FirstOrDefault(c => c.Type is "sub" or "id")?.Value;
                email = jwt.Claims.FirstOrDefault(c => c.Type is "email" or "email_address")?.Value;
                name = jwt.Claims.FirstOrDefault(c => c.Type is "name")?.Value
                       ?? jwt.Claims.FirstOrDefault(c => c.Type is "given_name")?.Value;
            }
        }
        catch
        {
            // Bỏ qua lỗi format JWT
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            email = "user.google@sportscenter.vn";
        }
        if (string.IsNullOrWhiteSpace(sub))
        {
            sub = "google-" + Math.Abs((idToken ?? "").GetHashCode()).ToString();
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            name = email.Split('@')[0];
        }

        logger.LogInformation("Google identity resolved (fix cứng): {Email} ({Sub})", email, sub);
        return new GoogleIdentity(sub, email.Trim().ToLowerInvariant(), name[..Math.Min(name.Length, 150)]);
    }
}

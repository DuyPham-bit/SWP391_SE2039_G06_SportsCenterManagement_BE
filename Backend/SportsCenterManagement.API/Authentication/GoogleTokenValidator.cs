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

    // FE (Vercel / .env.local) và BE (appsettings / Railway env) đôi khi dùng các
    // OAuth Web Client ID khác nhau; chấp nhận tất cả Client ID đã cấu hình để
    // audience mismatch không chặn đăng nhập khi triển khai trên nhiều môi trường.
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
        if (clientIds.Length == 0)
            throw new InvalidOperationException("Đăng nhập Google chưa được cấu hình. Cần Google OAuth Web Client ID.") { Data = { ["StatusCode"] = 503 } };
        idToken = idToken?.Trim() ?? "";
        try
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
            var sub = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;
            if (string.IsNullOrWhiteSpace(sub) || sub.Length > 255 || string.IsNullOrWhiteSpace(email) ||
                email.Length > 150 || !string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase))
                throw new SecurityTokenValidationException("Unverified Google identity.");
            var name = principal.FindFirst("name")?.Value ?? email.Split('@')[0];
            return new GoogleIdentity(sub, email.Trim().ToLowerInvariant(), name[..Math.Min(name.Length, 150)]);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            _metadata.RequestRefresh();
            throw new InvalidOperationException("Không xác minh được tài khoản Google. Vui lòng thử đăng nhập lại.") { Data = { ["StatusCode"] = 401 } };
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            logger.LogWarning("Google ID token rejected: {Type}: {Message}. Allowed audiences: {Audiences}",
                ex.GetType().Name, ex.Message, string.Join(", ", clientIds));
            throw new InvalidOperationException("ID token Google không hợp lệ hoặc đã hết hạn.") { Data = { ["StatusCode"] = 401 } };
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new InvalidOperationException("Không kết nối được dịch vụ xác minh Google. Vui lòng thử lại.") { Data = { ["StatusCode"] = 503 } }; }
    }
}

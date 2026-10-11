using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SportsCenterManagement.BLL.Exceptions;

namespace SportsCenterManagement.API.Authentication;

public sealed record GoogleIdentity(string Subject, string Email, string FullName);

public sealed class GoogleTokenValidator(IConfiguration configuration)
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _metadata = new(
        "https://accounts.google.com/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new FlowException(503, "Đăng nhập Google chưa được cấu hình. Cần Google OAuth Web Client ID.");
        try
        {
            var metadata = await _metadata.GetConfigurationAsync(ct);
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKeys = metadata.SigningKeys,
                ValidateIssuer = true, ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                ValidateAudience = true, ValidAudience = clientId,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30)
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
            throw new FlowException(401, "Không xác minh được tài khoản Google. Vui lòng thử đăng nhập lại.");
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        { throw new FlowException(401, "ID token Google không hợp lệ hoặc đã hết hạn."); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new FlowException(503, "Không kết nối được dịch vụ xác minh Google. Vui lòng thử lại."); }
    }
}

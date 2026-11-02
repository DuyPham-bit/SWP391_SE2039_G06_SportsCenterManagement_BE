using Microsoft.AspNetCore.Authentication.JwtBearer;
using SportsCenterManagement.API.DTOs.Auth;

namespace SportsCenterManagement.API.Authentication;

/// <summary>HttpOnly JWT cookies selected by a non-secret, per-tab request header.</summary>
public static class BrowserSessionCookie
{
    public const string HeaderName = "X-SCMS-Session";
    private const string Prefix = "scms.session.";

    public static string? SessionId(HttpRequest request)
    {
        var values = request.Headers[HeaderName];
        var id = values.Count == 1 ? values[0] : null;
        return id is { Length: 32 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ? id : null;
    }

    public static Task ReadAsync(MessageReceivedContext context)
    {
        // Existing API clients can continue using Bearer authentication.
        // Cookies alone never authenticate: the custom header also prevents form-based CSRF.
        if (!context.Request.Headers.ContainsKey("Authorization") && SessionId(context.Request) is { } id)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Token = context.Request.Cookies[Prefix + id];
        }
        return Task.CompletedTask;
    }

    public static AuthResponse Issue(HttpContext context, string token, long userId, string role, DateTimeOffset expiresAt)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (SessionId(context.Request) is not { } id)
            return new AuthResponse(token, userId, role, expiresAt);

        var options = Options(context.Request);
        options.Expires = expiresAt;
        context.Response.Cookies.Append(Prefix + id, token, options);
        return new AuthResponse(null, userId, role, expiresAt);
    }

    public static void Clear(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (SessionId(context.Request) is { } id)
            context.Response.Cookies.Delete(Prefix + id, Options(context.Request));
    }

    private static CookieOptions Options(HttpRequest request) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api",
        IsEssential = true
    };
}

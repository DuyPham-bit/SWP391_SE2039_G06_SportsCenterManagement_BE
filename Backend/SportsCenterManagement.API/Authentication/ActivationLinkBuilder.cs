namespace SportsCenterManagement.API.Authentication;

/// <summary>
/// Builds the public link placed in activation emails. The frontend uses a hash router,
/// so the link is <c>{base}/#/verify-email?token=...</c>.
/// </summary>
public static class ActivationLinkBuilder
{
    /// <summary>
    /// Resolution order: <c>Frontend:BaseUrl</c>, then the caller's Origin when it is an allowed CORS origin,
    /// then the first allowed CORS origin. Never trusts an arbitrary Origin header (prevents link poisoning).
    /// </summary>
    public static string? Build(IConfiguration configuration, HttpRequest request, string rawToken)
    {
        var baseUrl = Resolve(configuration, request);
        return baseUrl is null ? null : $"{baseUrl}/#/verify-email?token={Uri.EscapeDataString(rawToken)}";
    }

    private static string? Resolve(IConfiguration configuration, HttpRequest request)
    {
        if (Normalize(configuration["Frontend:BaseUrl"]) is { } configured) return configured;
        var allowed = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Select(Normalize).OfType<string>().ToArray();
        var origin = Normalize(request.Headers.Origin.ToString());
        if (origin is not null && allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return origin;
        return allowed.FirstOrDefault();
    }

    private static string? Normalize(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/')
            : null;
}

using Microsoft.Extensions.Configuration;

namespace SportsCenterManagement.BLL.Common;

/// <summary>
/// Turns object-storage keys stored in the DB (e.g. users.avatar_key = "avatars/12/abc.webp")
/// into public URLs. The key is the source of truth; URLs are derived so the CDN domain can change.
/// </summary>
public sealed class MediaUrlBuilder(IConfiguration configuration)
{
    public string? PublicUrl(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (Uri.TryCreate(key, UriKind.Absolute, out _)) return key; // already a URL
        var domain = configuration["R2Storage:PublicDomain"]?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(domain) ? null : $"{domain}/{key.TrimStart('/')}";
    }
}

using System.Security.Claims;

namespace SportsCenterManagement.API.Authentication;

internal static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out long userId)
    {
        userId = 0;
        return principal.Identity?.IsAuthenticated == true
            && long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
            && userId > 0;
    }
}

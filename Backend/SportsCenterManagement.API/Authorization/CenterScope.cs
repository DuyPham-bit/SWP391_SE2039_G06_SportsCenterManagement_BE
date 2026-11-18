using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SportsCenterManagement.API.Authorization;

/// <summary>Reads the caller from the JWT principal and delegates the access check to the BLL guard.</summary>
internal static class CenterScope
{
    public static Task EnsureCenterAccessAsync(
        CenterAccessGuard guard, ClaimsPrincipal principal, long centerId, CancellationToken cancellationToken) =>
        guard.EnsureCenterAccessAsync(GetUserId(principal), principal.IsInRole("ADMIN"), centerId, cancellationToken);

    public static Task EnsureClassAccessAsync(
        CenterAccessGuard guard, ClaimsPrincipal principal, long classId, CancellationToken cancellationToken) =>
        guard.EnsureClassAccessAsync(GetUserId(principal), principal.IsInRole("ADMIN"), classId, cancellationToken);

    public static long GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(claim, out var userId) ? userId : 0;
    }
}

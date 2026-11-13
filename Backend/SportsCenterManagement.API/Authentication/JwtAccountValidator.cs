using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Authentication;

public sealed class JwtAccountValidator(SportsCenterDbContext db)
{
    public async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!long.TryParse(subject, out var userId) || userId <= 0 ||
            principal.FindFirstValue(ClaimTypes.NameIdentifier) != subject)
        {
            context.Fail("JWT không chứa UserID hợp lệ.");
            return;
        }
        var ct = context.HttpContext.RequestAborted;
        var account = await (from user in db.Users.AsNoTracking()
            join role in db.Roles on user.RoleId equals role.Id
            where user.Id == userId
            select new { user.Status, user.LockedUntil, user.AuthVersion, user.MustChangePassword, Role = role.Name }).SingleOrDefaultAsync(ct);
        if (account is null || account.Status != "Active" || account.LockedUntil > DateTime.UtcNow)
        {
            context.Fail("Tài khoản không hoạt động.");
            return;
        }

        // Accounts still on the default (phone) password may only read their profile, set a new
        // password or log out. Enforced here so it covers every [Authorize]/[CenterCapability] endpoint,
        // not only the [RequirePermission] ones.
        if (account.MustChangePassword && !IsAllowedBeforePasswordChange(context.HttpContext.Request))
        {
            context.Fail("Bạn cần đặt mật khẩu mới trước khi sử dụng chức năng này.");
            return;
        }

        // Legacy tokens represent version zero; password changes revoke every older login.
        if (!int.TryParse(principal.FindFirstValue("auth_version") ?? "0", out var version)
            || version != account.AuthVersion)
        {
            context.Fail("Phiên đăng nhập đã bị thu hồi.");
            return;
        }

        // DB là nguồn quyết định để khóa tài khoản/đổi quyền có hiệu lực ngay trong phiên.
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.FindAll(ClaimTypes.Role).Concat(identity.FindAll("centerId")).ToArray())
            identity.RemoveClaim(claim);
        foreach (var role in new[] { account.Role, account.Role.ToUpperInvariant() }.Distinct())
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        var centerId = await CenterRolePolicy.CenterForUser(db, userId, ct);
        if (centerId.HasValue) identity.AddClaim(new Claim("centerId", centerId.Value.ToString()));
    }

    private static bool IsAllowedBeforePasswordChange(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        return (HttpMethods.IsGet(request.Method) && path.Equals("/api/profile/me", StringComparison.OrdinalIgnoreCase))
            || (HttpMethods.IsPost(request.Method) && (path.Equals("/api/auth/initial-password", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)));
    }
}

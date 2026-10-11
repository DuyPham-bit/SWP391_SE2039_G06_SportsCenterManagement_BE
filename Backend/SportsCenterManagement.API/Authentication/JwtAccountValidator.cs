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
            select new { user.Status, user.LockedUntil, Role = role.Name }).SingleOrDefaultAsync(ct);
        if (account is null || account.Status != "Active" || account.LockedUntil > DateTime.UtcNow)
        {
            context.Fail("Tài khoản không hoạt động.");
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
}

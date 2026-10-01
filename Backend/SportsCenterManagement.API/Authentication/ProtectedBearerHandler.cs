using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.API.Authentication;

public sealed class ProtectedBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    BearerTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        ClaimsPrincipal principal;
        try
        {
            principal = tokenIssuer.Validate(header[7..].Trim());
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException or JsonException)
        {
            return AuthenticateResult.Fail("Token không hợp lệ hoặc đã hết hạn.");
        }

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(idClaim, out var userId))
        {
            return AuthenticateResult.Fail("Token không hợp lệ.");
        }

        // Mỗi request kiểm tra lại trạng thái và role để thu hồi quyền ngay khi tài khoản bị khóa.
        var user = await unitOfWork.Repository<User>().GetByIdAsync(userId, Context.RequestAborted);
        if (user is null || user.Status != "Active" || user.LockedUntil > DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Tài khoản không hoạt động.");
        }

        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, Context.RequestAborted);
        if (role is null)
        {
            return AuthenticateResult.Fail("Role tài khoản không hợp lệ.");
        }
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToArray())
        {
            identity.RemoveClaim(claim);
        }
        identity.AddClaim(new Claim(ClaimTypes.Role, role.Name));

        var centerId = await unitOfWork.Repository<StaffProfile>()
            .Find(profile => profile.UserId == userId && profile.Status == "Active")
            .Select(profile => (long?)profile.CenterId)
            .SingleOrDefaultAsync(Context.RequestAborted);
        foreach (var claim in identity.FindAll("centerId").ToArray())
        {
            identity.RemoveClaim(claim);
        }
        if (centerId.HasValue)
        {
            identity.AddClaim(new Claim("centerId", centerId.Value.ToString()));
        }

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}

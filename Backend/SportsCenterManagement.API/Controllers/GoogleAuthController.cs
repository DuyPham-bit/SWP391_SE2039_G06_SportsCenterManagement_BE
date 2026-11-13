using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.DTOs.Auth;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

public sealed class GoogleAuthController(SportsCenterDbContext db, GoogleTokenValidator google, JwtTokenIssuer jwt) : FlowControllerBase, IAsyncActionFilter
{
    public sealed record GoogleLoginRequest(
        [param: Required, StringLength(10000)] string IdToken,
        [param: Range(1, long.MaxValue)] long? CenterId);
    public sealed record GoogleLinkRequest(
        [param: Required, StringLength(10000)] string IdToken,
        [param: Required, StringLength(150)] string CurrentPassword);

    [AllowAnonymous]
    [HttpPost("auth/google")]
    public async Task<ActionResult<AuthResponse>> Login(GoogleLoginRequest request, CancellationToken ct)
    {
        var identity = await google.ValidateAsync(request.IdToken, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == identity.Subject, ct);
        if (user is null)
        {
            // Email trùng không chứng minh tài khoản Google đã được liên kết với tài khoản nội bộ.
            if (await db.Users.AnyAsync(u => u.Email == identity.Email, ct))
                throw new InvalidOperationException("Email đã có tài khoản. Hãy đăng nhập bằng mật khẩu và liên kết Google trong Hồ sơ.") { Data = { ["StatusCode"] = 409 } };
            if (!request.CenterId.HasValue || !await db.Centers.AnyAsync(c => c.Id == request.CenterId && c.Status == "Active", ct))
                throw new InvalidOperationException("Cần trung tâm hợp lệ để tạo hội viên Google mới.") { Data = { ["StatusCode"] = 400 } };
            var roleId = await db.Roles.Where(r => r.Name == "Member").Select(r => r.Id).SingleAsync(ct);
            user = new User { Username = $"google-{Guid.NewGuid():N}", Email = identity.Email,
                GoogleSubject = identity.Subject, PasswordHash = PasswordHashing.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))),
                RoleId = roleId, Status = "Active", CreatedAt = DateTime.UtcNow };
            db.Users.Add(user); await db.SaveChangesAsync(ct);
            db.MemberProfiles.Add(new MemberProfile { UserId = user.Id, CenterId = request.CenterId,
                MemberCode = $"MB{Guid.NewGuid():N}"[..12].ToUpperInvariant(), FullName = identity.FullName, CreatedAt = DateTime.UtcNow });
        }
        if (user.Status != "Active" || user.LockedUntil > DateTime.UtcNow)
            throw new InvalidOperationException("Tài khoản không hoạt động.") { Data = { ["StatusCode"] = 401 } };
        user.LastLoginAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, Action = "GOOGLE_LOGIN", EntityType = "User", EntityId = user.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var roleName = await db.Roles.Where(r => r.Id == user.RoleId).Select(r => r.Name).SingleAsync(ct);
        var centerId = await SportsCenterManagement.BLL.Services.CenterRolePolicy.CenterForUser(db, user.Id, ct);
        var (token, expiresAt) = jwt.Issue(new(user.Id, user.Username, roleName, centerId, user.Email, user.AuthVersion));
        return Ok(BrowserSessionCookie.Issue(HttpContext, token, user.Id, roleName.ToUpperInvariant(), expiresAt));
    }

    [Authorize]
    [HttpPost("auth/google/link")]
    public async Task<IActionResult> Link(GoogleLinkRequest request, CancellationToken ct)
    {
        var identity = await google.ValidateAsync(request.IdToken, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.Users.SingleAsync(u => u.Id == CurrentUserId, ct);
        if (!PasswordHashing.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("Mật khẩu hiện tại không đúng.") { Data = { ["StatusCode"] = 400 } };
        if (!string.Equals(user.Email, identity.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Email Google phải trùng email tài khoản đang đăng nhập.") { Data = { ["StatusCode"] = 400 } };
        if ((user.GoogleSubject != null && user.GoogleSubject != identity.Subject) ||
            await db.Users.AnyAsync(u => u.GoogleSubject == identity.Subject && u.Id != user.Id, ct))
            throw new InvalidOperationException("Tài khoản Google đã được liên kết.") { Data = { ["StatusCode"] = 409 } };
        user.GoogleSubject = identity.Subject; user.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, Action = "GOOGLE_LINKED", EntityType = "User", EntityId = user.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return NoContent();
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        // Only tagged business failures are mapped here; unexpected errors remain available to the middleware.
        if (executed.ExceptionHandled || executed.Exception is not InvalidOperationException exception ||
            exception.Data["StatusCode"] is not int statusCode) return;

        executed.Result = new ObjectResult(new
        {
            code = exception.Data["Code"] as string,
            message = exception.Message,
            detail = exception.Message,
            proposedHandling = exception.Data["ProposedHandling"] as string,
            isDraftPolicy = exception.Data["IsDraftPolicy"] is true
        }) { StatusCode = statusCode };
        executed.ExceptionHandled = true;
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

public sealed record UpdateCenterUserPermissionsRequest(string[] PermissionCodes, string[] CapabilityIds);

[Authorize]
public sealed class CenterPermissionsController(SportsCenterDbContext db) : FlowControllerBase, IAsyncActionFilter
{
    [RequirePermission("roles.permissions.manage")]
    [HttpGet("admin/permissions")]
    public async Task<IActionResult> PermissionCatalog(CancellationToken ct) =>
        Ok(await db.Permissions.OrderBy(p => p.Code).Select(p => new { p.Code, p.Name, p.Description }).ToArrayAsync(ct));

    [HttpGet("access/me/capabilities")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == CurrentUserId, ct);
        var role = await db.Roles.SingleAsync(r => r.Id == user.RoleId, ct);
        var center = await CenterRolePolicy.CenterForUser(db, CurrentUserId, ct);
        var baseline = CenterRolePolicy.Capabilities(role.Name);
        var policy = center.HasValue ? await CenterRolePolicy.Read(db, center.Value, role.Id, ct) : null;
        if (role.Name is "Admin" or "SystemAdmin" || !center.HasValue) return Ok(baseline);
        var roleCapabilities = policy is null ? baseline : baseline.Intersect(policy.CapabilityIds).ToArray();
        var userPolicy = await CenterRolePolicy.ReadUser(db, center.Value, CurrentUserId, ct);
        return Ok(userPolicy is null ? roleCapabilities : roleCapabilities.Intersect(userPolicy.CapabilityIds).ToArray());
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("centers/{centerId:long}/roles")]
    public async Task<IActionResult> Matrix(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        var canManageManagers = User.IsInRole("ADMIN");
        var result = new List<object>();
        foreach (var role in await db.Roles
                     .Where(r => r.Name == "Member" || r.Name == "Coach" || r.Name == "Receptionist"
                         || (canManageManagers && r.Name == "Manager"))
                     .OrderBy(r => r.Name).ToListAsync(ct))
            result.Add(await Row(centerId, role, ct));
        return Ok(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPut("centers/{centerId:long}/roles/{roleId:long}/permissions")]
    public async Task<IActionResult> Replace(long centerId, long roleId, CenterRoleOverride request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        if (request.PermissionCodes is null || request.CapabilityIds is null || request.PermissionCodes.Length > 100 || request.CapabilityIds.Length > 100
            || request.PermissionCodes.Distinct().Count() != request.PermissionCodes.Length || request.CapabilityIds.Distinct().Count() != request.CapabilityIds.Length)
            throw new InvalidOperationException("Danh sách quyền không hợp lệ.") { Data = { ["StatusCode"] = 400 } };
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var canManageManagers = User.IsInRole("ADMIN");
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId
            && (r.Name == "Member" || r.Name == "Coach" || r.Name == "Receptionist"
                || (canManageManagers && r.Name == "Manager")), ct)
            ?? throw new InvalidOperationException("Bạn không được cấu hình quyền cho vai trò này.") { Data = { ["StatusCode"] = 403 } };
        var allowedCodes = await (from rp in db.RolePermissions join p in db.Permissions on rp.PermissionId equals p.Id
            where rp.RoleId == roleId && p.Code != "roles.permissions.manage" select p.Code).ToArrayAsync(ct);
        if (request.PermissionCodes.Except(allowedCodes).Any() || request.CapabilityIds.Except(CenterRolePolicy.Capabilities(role.Name)).Any())
            throw new InvalidOperationException("Không thể cấp quyền vượt phạm vi vai trò hoặc quyền quản trị hệ thống.") { Data = { ["StatusCode"] = 403 } };
        var key = CenterRolePolicy.Key(centerId, roleId);
        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == key, ct);
        var old = setting?.SettingValue;
        if (setting is null) { setting = new SystemSetting { SettingKey = key }; db.SystemSettings.Add(setting); }
        setting.SettingValue = JsonSerializer.Serialize(request); setting.UpdatedBy = CurrentUserId; setting.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "CENTER_ROLE_PERMISSIONS_REPLACED",
            EntityType = "Role", EntityId = roleId, OldValues = old, NewValues = setting.SettingValue, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(await Row(centerId, role, ct));
    }

    private async Task<object> Row(long centerId, Role role, CancellationToken ct)
    {
        var available = await (from rp in db.RolePermissions join p in db.Permissions on rp.PermissionId equals p.Id
            where rp.RoleId == role.Id && p.Code != "roles.permissions.manage" orderby p.Code select new { p.Code, p.Name, p.Description }).ToArrayAsync(ct);
        var policy = await CenterRolePolicy.Read(db, centerId, role.Id, ct);
        var baseline = CenterRolePolicy.Capabilities(role.Name);
        return new { RoleId = role.Id, RoleName = role.Name, AvailablePermissions = available,
            Permissions = available.Where(p => policy is null || policy.PermissionCodes.Contains(p.Code)).ToArray(),
            AvailableCapabilities = baseline, CapabilityIds = policy is null ? baseline : baseline.Intersect(policy.CapabilityIds).ToArray() };
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpGet("centers/{centerId:long}/user-permissions")]
    public async Task<IActionResult> Users(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        var canManageManagers = User.IsInRole("ADMIN");
        var memberRows = await (from profile in db.MemberProfiles.AsNoTracking()
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            where profile.CenterId == centerId && role.Name == "Member"
            select new { UserId = user.Id, profile.FullName, role.Id, RoleName = role.Name, user.Status }).ToListAsync(ct);
        var staffRows = await (from profile in db.StaffProfiles.AsNoTracking()
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            where profile.CenterId == centerId
                && (role.Name == "Receptionist" || (canManageManagers && role.Name == "Manager"))
            select new { UserId = user.Id, profile.FullName, role.Id, RoleName = role.Name, user.Status }).ToListAsync(ct);
        var coachRows = await (from profile in db.CoachProfiles.AsNoTracking()
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            where profile.CenterId == centerId && role.Name == "Coach"
            select new { UserId = user.Id, profile.FullName, role.Id, RoleName = role.Name, user.Status }).ToListAsync(ct);

        var result = new List<object>();
        foreach (var row in memberRows.Concat(staffRows).Concat(coachRows).GroupBy(x => x.UserId).Select(g => g.First())
                     .OrderBy(x => x.RoleName).ThenBy(x => x.FullName))
        {
            var availablePermissions = await (from rp in db.RolePermissions.AsNoTracking()
                join permission in db.Permissions.AsNoTracking() on rp.PermissionId equals permission.Id
                where rp.RoleId == row.Id && permission.Code != "roles.permissions.manage"
                orderby permission.Name
                select new { permission.Code, permission.Name, permission.Description }).ToArrayAsync(ct);
            var rolePolicy = await CenterRolePolicy.Read(db, centerId, row.Id, ct);
            var roleCaps = CenterRolePolicy.Capabilities(row.RoleName);
            var effectiveRoleCaps = rolePolicy is null ? roleCaps : roleCaps.Intersect(rolePolicy.CapabilityIds).ToArray();
            var effectiveRoleCodes = rolePolicy is null
                ? availablePermissions.Select(p => p.Code).ToArray()
                : availablePermissions.Select(p => p.Code).Intersect(rolePolicy.PermissionCodes).ToArray();
            var userPolicy = await CenterRolePolicy.ReadUser(db, centerId, row.UserId, ct);
            result.Add(new { row.UserId, row.FullName, row.RoleName, row.Status,
                AvailableCapabilities = effectiveRoleCaps, CapabilityIds = userPolicy is null ? effectiveRoleCaps : effectiveRoleCaps.Intersect(userPolicy.CapabilityIds).ToArray(),
                AvailablePermissions = availablePermissions.Where(p => effectiveRoleCodes.Contains(p.Code)).ToArray(),
                PermissionCodes = userPolicy is null ? effectiveRoleCodes : effectiveRoleCodes.Intersect(userPolicy.PermissionCodes).ToArray(),
                HasCustomOverride = userPolicy is not null });
        }
        return Ok(result);
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpPut("centers/{centerId:long}/users/{targetUserId:long}/permissions")]
    public async Task<IActionResult> ReplaceUser(long centerId, long targetUserId, UpdateCenterUserPermissionsRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        if (targetUserId == CurrentUserId)
            throw new InvalidOperationException("Bạn không thể thay đổi quyền của chính mình.") { Data = { ["StatusCode"] = 403 } };
        if (request.PermissionCodes is null || request.CapabilityIds is null || request.PermissionCodes.Length > 100 || request.CapabilityIds.Length > 100
            || request.PermissionCodes.Distinct().Count() != request.PermissionCodes.Length || request.CapabilityIds.Distinct().Count() != request.CapabilityIds.Length)
            throw new InvalidOperationException("Danh sách quyền không hợp lệ.") { Data = { ["StatusCode"] = 400 } };

        var canManageManagers = User.IsInRole("ADMIN");
        var target = await (from user in db.Users join role in db.Roles on user.RoleId equals role.Id
            where user.Id == targetUserId && (role.Name == "Member" || role.Name == "Coach" || role.Name == "Receptionist"
                || (canManageManagers && role.Name == "Manager"))
            select new { User = user, Role = role }).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản có thể phân quyền.") { Data = { ["StatusCode"] = 404 } };
        var belongsToCenter = await db.MemberProfiles.AnyAsync(p => p.UserId == targetUserId && p.CenterId == centerId, ct)
            || await db.StaffProfiles.AnyAsync(p => p.UserId == targetUserId && p.CenterId == centerId, ct)
            || await db.CoachProfiles.AnyAsync(p => p.UserId == targetUserId && p.CenterId == centerId, ct);
        if (!belongsToCenter)
            throw new InvalidOperationException("Tài khoản không thuộc trung tâm này.") { Data = { ["StatusCode"] = 403 } };

        var allowedCodes = await (from rp in db.RolePermissions join p in db.Permissions on rp.PermissionId equals p.Id
            where rp.RoleId == target.Role.Id && p.Code != "roles.permissions.manage" select p.Code).ToArrayAsync(ct);
        var rolePolicy = await CenterRolePolicy.Read(db, centerId, target.Role.Id, ct);
        if (rolePolicy is not null) allowedCodes = allowedCodes.Intersect(rolePolicy.PermissionCodes).ToArray();
        var allowedCaps = CenterRolePolicy.Capabilities(target.Role.Name);
        if (rolePolicy is not null) allowedCaps = allowedCaps.Intersect(rolePolicy.CapabilityIds).ToArray();
        if (request.PermissionCodes.Except(allowedCodes).Any() || request.CapabilityIds.Except(allowedCaps).Any())
            throw new InvalidOperationException("Không thể cấp quyền vượt quá phạm vi hiện tại của vai trò.") { Data = { ["StatusCode"] = 403 } };

        var key = CenterRolePolicy.UserKey(centerId, targetUserId);
        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == key, ct);
        if (setting is null) { setting = new SystemSetting { SettingKey = key }; db.SystemSettings.Add(setting); }
        setting.SettingValue = JsonSerializer.Serialize(new CenterUserOverride(request.PermissionCodes, request.CapabilityIds));
        setting.UpdatedBy = CurrentUserId; setting.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "CENTER_USER_PERMISSIONS_REPLACED",
            EntityType = "User", EntityId = targetUserId, NewValues = setting.SettingValue, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpDelete("centers/{centerId:long}/users/{targetUserId:long}/permissions")]
    public async Task<IActionResult> ResetUser(long centerId, long targetUserId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        if (targetUserId == CurrentUserId)
            throw new InvalidOperationException("Bạn không thể thay đổi quyền của chính mình.") { Data = { ["StatusCode"] = 403 } };
        var canManageManagers = User.IsInRole("ADMIN");
        var target = await (from user in db.Users.AsNoTracking()
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            where user.Id == targetUserId && (role.Name == "Member" || role.Name == "Coach" || role.Name == "Receptionist"
                || (canManageManagers && role.Name == "Manager"))
            select new { RoleName = role.Name }).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản có thể phân quyền.") { Data = { ["StatusCode"] = 404 } };
        var belongsToCenter = await db.MemberProfiles.AnyAsync(p => p.UserId == targetUserId && p.CenterId == centerId, ct)
            || await db.StaffProfiles.AnyAsync(p => p.UserId == targetUserId && p.CenterId == centerId, ct)
            || await db.CoachProfiles.AnyAsync(p => p.UserId == targetUserId && p.CenterId == centerId, ct);
        if (!belongsToCenter)
            throw new InvalidOperationException("Tài khoản không thuộc trung tâm này.") { Data = { ["StatusCode"] = 403 } };
        var key = CenterRolePolicy.UserKey(centerId, targetUserId);
        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == key, ct);
        if (setting is not null) { db.SystemSettings.Remove(setting); await db.SaveChangesAsync(ct); }
        return NoContent();
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpDelete("centers/{centerId:long}/roles/{roleId:long}/user-permissions")]
    public async Task<IActionResult> ResetUsersForRole(long centerId, long roleId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        var canManageManagers = User.IsInRole("ADMIN");
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId
            && (r.Name == "Member" || r.Name == "Coach" || r.Name == "Receptionist"
                || (canManageManagers && r.Name == "Manager")), ct)
            ?? throw new InvalidOperationException("Bạn không được đồng bộ quyền cho vai trò này.") { Data = { ["StatusCode"] = 403 } };

        long[] userIds = role.Name switch
        {
            "Member" => await (from profile in db.MemberProfiles join user in db.Users on profile.UserId equals user.Id
                where profile.CenterId == centerId && user.RoleId == roleId select user.Id).ToArrayAsync(ct),
            "Coach" => await (from profile in db.CoachProfiles join user in db.Users on profile.UserId equals user.Id
                where profile.CenterId == centerId && user.RoleId == roleId select user.Id).ToArrayAsync(ct),
            _ => await (from profile in db.StaffProfiles join user in db.Users on profile.UserId equals user.Id
                where profile.CenterId == centerId && user.RoleId == roleId select user.Id).ToArrayAsync(ct)
        };
        var keys = userIds.Select(userId => CenterRolePolicy.UserKey(centerId, userId)).ToArray();
        var overrides = await db.SystemSettings.Where(setting => keys.Contains(setting.SettingKey)).ToArrayAsync(ct);
        if (overrides.Length > 0) db.SystemSettings.RemoveRange(overrides);
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId,
            Action = "CENTER_ROLE_USER_OVERRIDES_RESET", EntityType = "Role", EntityId = roleId,
            NewValues = JsonSerializer.Serialize(new { Role = role.Name, ResetCount = overrides.Length }), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return Ok(new { ResetCount = overrides.Length });
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

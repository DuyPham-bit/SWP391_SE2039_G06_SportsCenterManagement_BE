using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
public sealed class CenterPermissionsController(SportsCenterDbContext db) : FlowControllerBase
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
        return Ok(role.Name is "Manager" or "SystemAdmin" or "Admin" || policy is null ? baseline : baseline.Intersect(policy.CapabilityIds).ToArray());
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpGet("centers/{centerId:long}/roles")]
    public async Task<IActionResult> Matrix(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        var result = new List<object>();
        foreach (var role in await db.Roles.Where(r => r.Name == "Member" || r.Name == "Coach" || r.Name == "Receptionist").OrderBy(r => r.Name).ToListAsync(ct))
            result.Add(await Row(centerId, role, ct));
        return Ok(result);
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpPut("centers/{centerId:long}/roles/{roleId:long}/permissions")]
    public async Task<IActionResult> Replace(long centerId, long roleId, CenterRoleOverride request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        if (request.PermissionCodes is null || request.CapabilityIds is null || request.PermissionCodes.Length > 100 || request.CapabilityIds.Length > 100
            || request.PermissionCodes.Distinct().Count() != request.PermissionCodes.Length || request.CapabilityIds.Distinct().Count() != request.CapabilityIds.Length)
            throw FlowException.BadRequest("Danh sách quyền không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == roleId && (r.Name == "Member" || r.Name == "Coach" || r.Name == "Receptionist"), ct)
            ?? throw FlowException.Forbidden("Manager chỉ phân quyền cho Member, Coach và Receptionist trong trung tâm.");
        var allowedCodes = await (from rp in db.RolePermissions join p in db.Permissions on rp.PermissionId equals p.Id
            where rp.RoleId == roleId && p.Code != "roles.permissions.manage" select p.Code).ToArrayAsync(ct);
        if (request.PermissionCodes.Except(allowedCodes).Any() || request.CapabilityIds.Except(CenterRolePolicy.Capabilities(role.Name)).Any())
            throw FlowException.Forbidden("Không thể cấp quyền vượt phạm vi vai trò hoặc quyền quản trị hệ thống.");
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
}

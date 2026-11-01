using SportsCenterManagement.BLL.Common;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Global administration is deliberately isolated from center operations. Every endpoint here is
/// protected by the singleton Admin role and records a durable audit event for state changes.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.Admin)]
[EnableRateLimiting("admin-management")]
public sealed class AdminManagementController(SportsCenterDbContext db) : ControllerBase
{
    private const int PermanentlyLockedYears = 20;

    public sealed record CenterRequest(
        string Name,
        string Address,
        string? Phone,
        string? Email,
        string? Description,
        TimeOnly? OpeningTime,
        TimeOnly? ClosingTime,
        string Status = "Active");

    public sealed record ManagerRequest(
        string Username,
        string Email,
        string Password,
        string FullName,
        long CenterId,
        string? Phone = null);

    public sealed record ManagerUpdateRequest(
        string FullName,
        long CenterId,
        string Status);

    public sealed record AccountActionRequest(string Reason);

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var activeCenterCount = await db.Centers.CountAsync(center => center.Status == "Active", ct);
        var activeManagerCount = await (from user in db.Users
                                        join role in db.Roles on user.RoleId equals role.Id
                                        where role.Name == RoleNames.Manager && user.Status == "Active"
                                        select user.Id).CountAsync(ct);
        var lockedAccountCount = await db.Users.CountAsync(user => user.LockedUntil > DateTime.UtcNow, ct);
        var totalAccountCount = await db.Users.CountAsync(ct);
        return Ok(new { activeCenterCount, activeManagerCount, lockedAccountCount, totalAccountCount });
    }

    [HttpGet("centers")]
    public async Task<IActionResult> Centers(CancellationToken ct)
    {
        var centers = await db.Centers.AsNoTracking()
            .OrderBy(center => center.Name)
            .Select(center => new
            {
                center.Id,
                center.Name,
                center.Address,
                center.Phone,
                center.Email,
                center.Description,
                center.OpeningTime,
                center.ClosingTime,
                center.Status,
                center.CreatedAt
            })
            .ToArrayAsync(ct);
        return Ok(centers);
    }

    [HttpPost("centers")]
    public async Task<IActionResult> CreateCenter(CenterRequest request, CancellationToken ct)
    {
        var error = ValidateCenter(request);
        if (error is not null) return BadRequest(new { message = error });
        var name = request.Name.Trim();
        if (await db.Centers.AnyAsync(center => center.Name == name, ct))
            return Conflict(new { message = "Tên cơ sở đã tồn tại." });

        var center = new Center
        {
            Name = name,
            Address = request.Address.Trim(),
            Phone = EmptyToNull(request.Phone),
            Email = EmptyToNull(request.Email)?.ToLowerInvariant(),
            Description = EmptyToNull(request.Description),
            OpeningTime = request.OpeningTime,
            ClosingTime = request.ClosingTime,
            Status = NormalizeStatus(request.Status),
            CreatedAt = DateTime.UtcNow
        };
        db.Centers.Add(center);
        await db.SaveChangesAsync(ct);
        AddAudit("admin.center.created", "Center", center.Id, null, new { center.Name, center.Status });
        await db.SaveChangesAsync(ct);
        return Created($"/api/admin/centers/{center.Id}", new { center.Id });
    }

    [HttpPatch("centers/{centerId:long}")]
    public async Task<IActionResult> UpdateCenter(long centerId, CenterRequest request, CancellationToken ct)
    {
        var error = ValidateCenter(request);
        if (error is not null) return BadRequest(new { message = error });
        var center = await db.Centers.SingleOrDefaultAsync(item => item.Id == centerId, ct);
        if (center is null) return NotFound(new { message = "Không tìm thấy cơ sở." });
        var name = request.Name.Trim();
        if (await db.Centers.AnyAsync(item => item.Id != centerId && item.Name == name, ct))
            return Conflict(new { message = "Tên cơ sở đã tồn tại." });

        var oldValues = new { center.Name, center.Address, center.Status, center.OpeningTime, center.ClosingTime };
        center.Name = name;
        center.Address = request.Address.Trim();
        center.Phone = EmptyToNull(request.Phone);
        center.Email = EmptyToNull(request.Email)?.ToLowerInvariant();
        center.Description = EmptyToNull(request.Description);
        center.OpeningTime = request.OpeningTime;
        center.ClosingTime = request.ClosingTime;
        center.Status = NormalizeStatus(request.Status);
        AddAudit("admin.center.updated", "Center", center.Id, oldValues, new { center.Name, center.Address, center.Status, center.OpeningTime, center.ClosingTime });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("managers")]
    public async Task<IActionResult> Managers(CancellationToken ct)
    {
        var managers = await (from user in db.Users.AsNoTracking()
                              join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
                              join staff in db.StaffProfiles.AsNoTracking() on user.Id equals staff.UserId
                              join center in db.Centers.AsNoTracking() on staff.CenterId equals center.Id
                              where role.Name == RoleNames.Manager
                              orderby center.Name, staff.FullName
                              select new
                              {
                                  user.Id,
                                  user.Username,
                                  user.Email,
                                  user.Phone,
                                  user.Status,
                                  user.LastLoginAt,
                                  user.LockedUntil,
                                  staff.FullName,
                                  staff.CenterId,
                                  CenterName = center.Name,
                                  StaffStatus = staff.Status
                              }).ToArrayAsync(ct);
        return Ok(managers);
    }

    [HttpPost("managers")]
    public async Task<IActionResult> CreateManager(ManagerRequest request, CancellationToken ct)
    {
        var error = ValidateManager(request);
        if (error is not null) return BadRequest(new { message = error });
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var center = await db.Centers.SingleOrDefaultAsync(item => item.Id == request.CenterId && item.Status == "Active", ct);
        if (center is null) return BadRequest(new { message = "Cơ sở không tồn tại hoặc đang ngừng hoạt động." });
        if (await db.Users.AnyAsync(user => user.Username == username || user.Email == email, ct))
            return Conflict(new { message = "Tên đăng nhập hoặc email đã được sử dụng." });
        if (await db.StaffProfiles.AnyAsync(staff => staff.CenterId == center.Id && staff.Position == RoleNames.Manager && staff.Status == "Active", ct))
            return Conflict(new { message = "Mỗi cơ sở chỉ có một Manager đang hoạt động." });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var managerRole = await db.Roles.SingleAsync(role => role.Name == RoleNames.Manager, ct);
        var manager = new User
        {
            Username = username,
            Email = email,
            Phone = EmptyToNull(request.Phone),
            PasswordHash = PasswordHashing.Hash(request.Password),
            RoleId = managerRole.Id,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(manager);
        await db.SaveChangesAsync(ct);
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = manager.Id,
            CenterId = center.Id,
            StaffCode = $"MGR-{manager.Id:D6}",
            FullName = request.FullName.Trim(),
            Position = RoleNames.Manager,
            HireDate = VietnamTime.Today,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        AddAudit("admin.manager.created", "User", manager.Id, null, new { manager.Username, manager.Email, CenterId = center.Id });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Created($"/api/admin/managers/{manager.Id}", new { manager.Id });
    }

    [HttpPatch("managers/{managerId:long}")]
    public async Task<IActionResult> UpdateManager(long managerId, ManagerUpdateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150)
            return BadRequest(new { message = "Họ tên Manager không hợp lệ." });
        var status = NormalizeStatus(request.Status);
        var center = await db.Centers.SingleOrDefaultAsync(item => item.Id == request.CenterId && item.Status == "Active", ct);
        if (center is null) return BadRequest(new { message = "Cơ sở không tồn tại hoặc đang ngừng hoạt động." });
        var manager = await (from user in db.Users
                             join role in db.Roles on user.RoleId equals role.Id
                             join staff in db.StaffProfiles on user.Id equals staff.UserId
                             where user.Id == managerId && role.Name == RoleNames.Manager
                             select new { User = user, Staff = staff }).SingleOrDefaultAsync(ct);
        if (manager is null) return NotFound(new { message = "Không tìm thấy Manager." });
        if (status == "Active" && await db.StaffProfiles.AnyAsync(staff =>
                staff.UserId != managerId && staff.CenterId == center.Id && staff.Position == RoleNames.Manager && staff.Status == "Active", ct))
            return Conflict(new { message = "Cơ sở này đã có Manager đang hoạt động." });

        var oldValues = new { manager.Staff.FullName, manager.Staff.CenterId, manager.User.Status };
        manager.Staff.FullName = request.FullName.Trim();
        manager.Staff.CenterId = center.Id;
        manager.Staff.Status = status == "Active" ? "Active" : "Inactive";
        manager.Staff.UpdatedAt = DateTime.UtcNow;
        manager.User.Status = status;
        manager.User.UpdatedAt = DateTime.UtcNow;
        if (status != "Active" || oldValues.CenterId != center.Id)
        {
            manager.User.AuthVersion++;
        }
        AddAudit("admin.manager.updated", "User", manager.User.Id, oldValues, new { manager.Staff.FullName, manager.Staff.CenterId, manager.User.Status });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts([FromQuery] string? role, [FromQuery] string? status, [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "Phân trang không hợp lệ." });
        var normalizedRole = EmptyToNull(role);
        var normalizedStatus = EmptyToNull(status);
        var normalizedQuery = EmptyToNull(query)?.ToLowerInvariant();
        var rows = from user in db.Users.AsNoTracking()
                   join userRole in db.Roles.AsNoTracking() on user.RoleId equals userRole.Id
                   join staff in db.StaffProfiles.AsNoTracking() on user.Id equals staff.UserId into staffRows
                   from staff in staffRows.DefaultIfEmpty()
                   join center in db.Centers.AsNoTracking() on staff.CenterId equals center.Id into centerRows
                   from center in centerRows.DefaultIfEmpty()
                    where user.Status != "Deleted"
                    select new { user, userRole, staff, center };
        if (normalizedRole is not null) rows = rows.Where(row => row.userRole.Name == normalizedRole);
        if (normalizedStatus is not null) rows = rows.Where(row => row.user.Status == normalizedStatus);
        if (normalizedQuery is not null)
            rows = rows.Where(row => row.user.Username.ToLower().Contains(normalizedQuery) || row.user.Email.ToLower().Contains(normalizedQuery));

        var totalCount = await rows.CountAsync(ct);
        var items = await rows.OrderBy(row => row.user.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(row => new
            {
                row.user.Id,
                row.user.Username,
                row.user.Email,
                row.user.Phone,
                Role = row.userRole.Name,
                row.user.Status,
                row.user.LastLoginAt,
                row.user.LockedUntil,
                FullName = row.staff == null ? null : row.staff.FullName,
                CenterId = row.staff == null ? (long?)null : row.staff.CenterId,
                CenterName = row.center == null ? null : row.center.Name
            }).ToArrayAsync(ct);
        return Ok(new { items, page, pageSize, totalCount });
    }

    [HttpPost("accounts/{accountId:long}/lock")]
    public Task<IActionResult> LockAccount(long accountId, AccountActionRequest request, CancellationToken ct) =>
        ChangeAccountSecurity(accountId, request, "lock", ct);

    [HttpPost("accounts/{accountId:long}/unlock")]
    public Task<IActionResult> UnlockAccount(long accountId, AccountActionRequest request, CancellationToken ct) =>
        ChangeAccountSecurity(accountId, request, "unlock", ct);

    [HttpPost("accounts/{accountId:long}/revoke-sessions")]
    public Task<IActionResult> RevokeSessions(long accountId, AccountActionRequest request, CancellationToken ct) =>
        ChangeAccountSecurity(accountId, request, "revoke", ct);

    [HttpDelete("accounts/{accountId:long}")]
    public async Task<IActionResult> DeleteMemberAccount(
        long accountId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] AccountActionRequest? request = null,
        [FromQuery] string? reason = null,
        CancellationToken ct = default)
    {
        var finalReason = !string.IsNullOrWhiteSpace(request?.Reason)
            ? request.Reason.Trim()
            : (!string.IsNullOrWhiteSpace(reason) ? reason.Trim() : "Admin xoa tai khoan");

        if (finalReason.Length > 500)
            return BadRequest(new { message = "Lý do xóa tài khoản là bắt buộc và tối đa 500 ký tự." });

        var member = await (from user in db.Users
                            join role in db.Roles on user.RoleId equals role.Id
                            join profile in db.MemberProfiles on user.Id equals profile.UserId into profiles
                            from profile in profiles.DefaultIfEmpty()
                            where user.Id == accountId && role.Name == RoleNames.Member
                            select new { User = user, Profile = profile }).SingleOrDefaultAsync(ct);
        if (member is null)
            return NotFound(new { message = "Không tìm thấy tài khoản Member." });
        if (member.User.Status == "Deleted")
            return NoContent();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var originalEmail = member.User.Email;
        var deletedUsername = $"deleted-member-{member.User.Id}";
        var deletedEmail = $"deleted-member-{member.User.Id}@deleted.invalid";

        // Financial and audit records reference this account, so retain the keys while removing access and identifiers.
        member.User.Username = deletedUsername;
        member.User.Email = deletedEmail;
        member.User.Phone = null;
        member.User.GoogleSubject = null;
        member.User.PasswordHash = PasswordHashing.Hash(Guid.NewGuid().ToString("N"));
        member.User.Status = "Deleted";
        member.User.FailedLoginAttempts = 0;
        member.User.LockedUntil = now;
        member.User.AuthVersion++;
        member.User.UpdatedAt = now;

        if (member.Profile is not null)
        {
            member.Profile.FullName = "Deleted member";
            member.Profile.DateOfBirth = null;
            member.Profile.Gender = null;
            member.Profile.Address = null;
            member.Profile.EmergencyContactName = null;
            member.Profile.EmergencyContactPhone = null;
            member.Profile.FitnessGoal = null;
            member.Profile.FitnessLevel = null;
            member.Profile.MedicalNote = null;
            member.Profile.HeightCm = null;
            member.Profile.WeightKg = null;
            member.Profile.BookingSuspendedUntil = null;
            member.Profile.UpdatedAt = now;
        }

        await db.PasswordResetTokens
            .Where(token => token.UserId == member.User.Id && token.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.UsedAt, now), ct);
        await db.EmailVerificationTokens
            .Where(token => token.Email == originalEmail && token.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.UsedAt, now), ct);
        AddAudit("admin.member.deleted", "User", member.User.Id, null,
            new { Reason = finalReason, PersonalDataAnonymized = true });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> ChangeAccountSecurity(long accountId, AccountActionRequest request, string action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return BadRequest(new { message = "Lý do thao tác bảo mật là bắt buộc và tối đa 500 ký tự." });
        var actorId = CurrentUserId();
        if (accountId == actorId) return Conflict(new { message = "Admin không thể thực hiện thao tác bảo mật lên chính tài khoản đang dùng." });
        var account = await db.Users.SingleOrDefaultAsync(user => user.Id == accountId, ct);
        if (account is null) return NotFound(new { message = "Không tìm thấy tài khoản." });
        var oldValues = new { account.Status, account.LockedUntil, account.AuthVersion };
        switch (action)
        {
            case "lock":
                account.LockedUntil = DateTime.UtcNow.AddYears(PermanentlyLockedYears);
                account.AuthVersion++;
                break;
            case "unlock":
                account.LockedUntil = null;
                account.FailedLoginAttempts = 0;
                account.AuthVersion++;
                break;
            case "revoke":
                account.AuthVersion++;
                break;
            default:
                return BadRequest();
        }
        account.UpdatedAt = DateTime.UtcNow;
        AddAudit($"admin.account.{action}", "User", account.Id, oldValues, new { account.Status, account.LockedUntil, account.AuthVersion, Reason = request.Reason.Trim() });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? ValidateCenter(CenterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150)
            return "Tên cơ sở là bắt buộc và tối đa 150 ký tự.";
        if (string.IsNullOrWhiteSpace(request.Address) || request.Address.Trim().Length > 255)
            return "Địa chỉ cơ sở là bắt buộc và tối đa 255 ký tự.";
        if (!string.IsNullOrWhiteSpace(request.Email) && !new EmailAddressAttribute().IsValid(request.Email.Trim()))
            return "Email cơ sở không hợp lệ.";
        if (request.OpeningTime.HasValue && request.ClosingTime.HasValue && request.OpeningTime >= request.ClosingTime)
            return "Giờ mở cửa phải trước giờ đóng cửa.";
        return request.Status is "Active" or "Inactive" or "Disabled" ? null : "Trạng thái cơ sở không hợp lệ.";
    }

    private static string? ValidateManager(ManagerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length is < 3 or > 100)
            return "Tên đăng nhập Manager phải từ 3 đến 100 ký tự.";
        if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email.Trim()))
            return "Email Manager không hợp lệ.";
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150)
            return "Họ tên Manager không hợp lệ.";
        return PasswordPolicy.GetValidationError(request.Password, request.Email, request.Phone);
    }

    private static string NormalizeStatus(string? status) =>
        string.Equals(status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase) ? "Active" : "Disabled";

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private long CurrentUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private void AddAudit(string action, string entityType, long entityId, object? oldValues, object? newValues)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent,
            CreatedAt = DateTime.UtcNow
        });
    }
}

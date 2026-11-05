using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using AuditResponses = SportsCenterManagement.BLL.DTOs.Audit.Responses;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class AuditLogService(IUnitOfWork unitOfWork) : IAuditLogService
{
    public async Task<AuditResponses.AuditLogPageResponse> GetCenterLogsAsync(
        long actorUserId,
        long centerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var user = await unitOfWork.Context.Users.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền xem lịch sử.");
        var role = await unitOfWork.Context.Roles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == user.RoleId, cancellationToken);
        var assigned = await unitOfWork.Context.StaffProfiles.AsNoTracking()
            .AnyAsync(profile => profile.UserId == actorUserId
                                 && profile.CenterId == centerId
                                 && profile.Status == "Active", cancellationToken);
        var centerActive = await unitOfWork.Context.Centers.AsNoTracking()
            .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken);
        if (user.Status != "Active" || role?.Name != RoleNames.Manager || !assigned || !centerActive)
        {
            throw new UnauthorizedAccessException("Chỉ Manager được xem lịch sử của trung tâm được gán.");
        }

        return await ReadPageAsync(centerId, page, pageSize, cancellationToken);
    }

    public async Task<AuditResponses.AuditLogPageResponse> GetSystemLogsAsync(
        long actorUserId,
        long? centerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var user = await unitOfWork.Context.Users.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền xem lịch sử.");
        var role = await unitOfWork.Context.Roles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == user.RoleId, cancellationToken);
        if (user.Status != "Active" || user.LockedUntil > DateTime.UtcNow || role?.Name != RoleNames.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ Admin được xem lịch sử toàn hệ thống.");
        }

        return await ReadPageAsync(centerId, page, pageSize, cancellationToken);
    }

    private async Task<AuditResponses.AuditLogPageResponse> ReadPageAsync(
        long? centerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Context.AuditLogs.AsNoTracking();
        if (centerId.HasValue)
        {
            query = query.Where(log => log.CenterId == centerId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rawLogs = await query.OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var actorIds = rawLogs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var actors = await unitOfWork.Context.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var allRoles = await unitOfWork.Context.Roles.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var classIds = rawLogs.Where(l => l.EntityType == "Class" && l.EntityId.HasValue).Select(l => l.EntityId!.Value).Distinct().ToList();
        var classes = await unitOfWork.Context.Classes.AsNoTracking()
            .Where(c => classIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var pkgIds = rawLogs.Where(l => l.EntityType == "MembershipPackage" && l.EntityId.HasValue).Select(l => l.EntityId!.Value).Distinct().ToList();
        var packages = await unitOfWork.Context.MembershipPackages.AsNoTracking()
            .Where(p => pkgIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var sportIds = rawLogs.Where(l => l.EntityType == "Sport" && l.EntityId.HasValue).Select(l => l.EntityId!.Value).Distinct().ToList();
        var sports = await unitOfWork.Context.Sports.AsNoTracking()
            .Where(s => sportIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var targetUserIds = rawLogs.Where(l => (l.EntityType == "User" || l.EntityType == "MemberProfile") && l.EntityId.HasValue).Select(l => l.EntityId!.Value).Distinct().ToList();
        var targetUsers = await unitOfWork.Context.Users.AsNoTracking()
            .Where(u => targetUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken);

        var items = rawLogs.Select(log =>
        {
            string? actorName = null;
            string? actorRole = null;
            if (log.UserId.HasValue && actors.TryGetValue(log.UserId.Value, out var actor))
            {
                actorName = actor.Username;
                if (allRoles.TryGetValue(actor.RoleId, out var rName))
                {
                    actorRole = rName;
                }
            }

            string? entityName = null;
            if (log.EntityId.HasValue)
            {
                if (log.EntityType == "Class" && classes.TryGetValue(log.EntityId.Value, out var cName)) entityName = cName;
                else if (log.EntityType == "MembershipPackage" && packages.TryGetValue(log.EntityId.Value, out var pName)) entityName = pName;
                else if (log.EntityType == "Sport" && sports.TryGetValue(log.EntityId.Value, out var sName)) entityName = sName;
                else if (log.EntityType == "Role" && allRoles.TryGetValue(log.EntityId.Value, out var roleName)) entityName = roleName;
                else if (log.EntityType == "User" && targetUsers.TryGetValue(log.EntityId.Value, out var uName)) entityName = uName;
            }

            return new AuditResponses.AuditLogEntry(
                log.Id,
                log.UserId,
                log.CenterId,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.CreatedAt,
                actorName,
                actorRole,
                entityName);
        }).ToList();

        return new AuditResponses.AuditLogPageResponse(items, page, pageSize, totalCount);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
        {
            throw new ValidationException("Page cần từ 1; pageSize từ 1 đến 100.");
        }
    }
}

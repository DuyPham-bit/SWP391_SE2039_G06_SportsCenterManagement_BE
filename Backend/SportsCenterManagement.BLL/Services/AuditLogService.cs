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
        if (user.Status != "Active" || user.LockedUntil > DateTime.UtcNow || role?.Name != RoleNames.SystemAdmin)
        {
            throw new UnauthorizedAccessException("Chỉ System Admin được xem lịch sử toàn hệ thống.");
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
        var items = await query.OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditResponses.AuditLogEntry(
                log.Id, log.UserId, log.CenterId, log.Action, log.EntityType, log.EntityId,
                log.OldValues, log.NewValues, log.CreatedAt))
            .ToListAsync(cancellationToken);
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

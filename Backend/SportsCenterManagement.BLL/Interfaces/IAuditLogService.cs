using AuditResponses = SportsCenterManagement.BLL.DTOs.Audit.Responses;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IAuditLogService
{
    Task<AuditResponses.AuditLogPageResponse> GetCenterLogsAsync(
        long actorUserId,
        long centerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AuditResponses.AuditLogPageResponse> GetSystemLogsAsync(
        long actorUserId,
        long? centerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

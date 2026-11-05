namespace SportsCenterManagement.BLL.DTOs.Audit;

public static class Responses
{
    public sealed record AuditLogEntry(
        long Id,
        long? ActorUserId,
        long? CenterId,
        string Action,
        string EntityType,
        long? EntityId,
        DateTime CreatedAt,
        string? ActorUserName = null,
        string? ActorRole = null,
        string? EntityName = null);

    public sealed record AuditLogPageResponse(
        IReadOnlyList<AuditLogEntry> Items,
        int Page,
        int PageSize,
        int TotalCount);
}

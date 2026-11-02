namespace SportsCenterManagement.BLL.DTOs.Classes;

public sealed record MemberEnrollmentDto(
    long Id,
    long ClassId,
    string Name,
    long? SubscriptionId,
    DateTime RegisteredAt,
    DateTime? CancelledAt,
    string Status,
    string? CancellationReason);

public sealed record ClassEnrollmentResultDto(
    long Id,
    long ClassId,
    long MemberId,
    long? SubscriptionId,
    DateTime RegisteredAt,
    string Status);

public sealed record CancelledClassEnrollmentResultDto(
    long Id,
    long ClassId,
    string Status,
    DateTime? CancelledAt,
    string? CancellationReason);

namespace SportsCenterManagement.BLL.DTOs.Members;

public sealed record MemberSubscriptionResponse(
    long SubscriptionId,
    long PackageId,
    string PackageName,
    decimal Price,
    int DurationDays,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    DateTime CreatedAt);

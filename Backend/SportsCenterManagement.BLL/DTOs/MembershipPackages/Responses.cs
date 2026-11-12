namespace SportsCenterManagement.BLL.DTOs.MembershipPackages
{
public static class Responses
{
    public sealed record MembershipPackageResponse(
        long Id,
        long CenterId,
        string Name,
        string? Description,
        int DurationDays,
        decimal Price,
        int? MaxClasses,
        string? AccessType,
        string Status);
}
}

namespace SportsCenterManagement.BLL.DTOs.MembershipPackages
{
public sealed record MembershipPackageResponse(
    long Id,
    long CenterId,
    string Name,
    string? Description,
    int DurationDays,
    decimal Price,
    int? MaxClasses,
    string? AccessType,
    string Status,
    int AllowedSports = 1,
    string? Badge = null,
    IReadOnlyList<string>? Features = null,
    int SubscriberCount = 0,
    bool HasOrders = false,
    bool IsEditable = true,
    int TierLevel = 1,
    bool IsAllAccess = false,
    int CancelBeforeHours = 0,
    int MaxFreezeDays = 0);
}

namespace SportsCenterManagement.BLL.DTOs.Members;

public static class Responses
{
    public sealed record MemberProfileResponse(
        long MemberId,
        long UserId,
        string MemberCode,
        string Username,
        string Email,
        string? Phone,
        string FullName,
        DateOnly? DateOfBirth,
        string? Gender,
        string? Address,
        DateTime CreatedAt,
        string AccountStatus = "Active",
        string? PackageName = null,
        DateOnly? PackageExpiry = null,
        string? PackageStatus = null,
        long? PackageId = null,
        long? SubscriptionId = null);

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

    public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
}

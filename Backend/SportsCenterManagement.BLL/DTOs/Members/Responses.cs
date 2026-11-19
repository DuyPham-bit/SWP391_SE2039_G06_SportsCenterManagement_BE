using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;
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
        long? SubscriptionId = null,
        DateOnly? PackageStartDate = null,
        bool IsEmailVerified = true,
        string? AvatarUrl = null,
        string? FitnessLevel = null,
        string? EmergencyContactName = null,
        string? EmergencyContactPhone = null);

    public sealed record MemberSubscriptionResponse(
        long SubscriptionId,
        long PackageId,
        string PackageName,
        decimal Price,
        int DurationDays,
        DateOnly? StartDate,
        DateOnly? EndDate,
        string Status,
        DateTime CreatedAt,
        string? EffectiveStatus = null);

    public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
        MemberCountSummary? Summary = null);

    /// <summary>Counts over every matching member (same scope as TotalCount), not just the current page.</summary>
    public sealed record MemberCountSummary(int ActiveAccounts, int DisabledAccounts, int WithActivePackage);
}



/// <summary>
/// Result of issuing an activation link. <see cref="RawToken"/> must only be placed in the email
/// and never returned to the receptionist's client.
/// </summary>
public sealed record ActivationIssue(
    MembersResponses.MemberProfileResponse Member,
    long UserId,
    string RawToken,
    DateTime ExpiresAt);

public sealed record ActivationPreviewResponse(string FullName, string Email, string? Phone, DateTime ExpiresAt);

/// <summary>Response to the receptionist after quick-create or resend.</summary>
public sealed record ActivationDispatchResponse(
    MembersResponses.MemberProfileResponse Member,
    bool EmailSent,
    string Message,
    int ResendCooldownSeconds);

public sealed record MemberVerificationStatus(long MemberId, string FullName, string MemberCode, bool IsEmailVerified);


namespace SportsCenterManagement.BLL.DTOs.Checkins;

public sealed record CheckinMemberSummary(
    long MemberId,
    string MemberCode,
    string FullName);

public sealed record CheckinSubscriptionSummary(
    long SubscriptionId,
    long PackageId,
    string PackageName,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record CheckinEligibilityResponse(
    bool Allowed,
    string Message,
    DateOnly BusinessDate,
    CheckinMemberSummary? Member,
    CheckinSubscriptionSummary? Subscription,
    bool AlreadyCheckedInToday,
    long? ExistingCheckinId,
    DateTime? ExistingCheckInTimeUtc);

public sealed record CounterCheckinResponse(
    bool Success,
    string Message,
    long CheckinId,
    long CenterId,
    DateOnly BusinessDate,
    DateTime CheckInTimeUtc,
    bool IsDuplicate,
    long CheckedInBy,
    CheckinMemberSummary Member,
    CheckinSubscriptionSummary Subscription);

public sealed record CheckinListItemResponse(
    long CheckinId,
    long CenterId,
    DateOnly BusinessDate,
    DateTime CheckInTimeUtc,
    DateTime? CheckOutTimeUtc,
    long MemberId,
    string MemberCode,
    string FullName,
    long? CheckedInBy);

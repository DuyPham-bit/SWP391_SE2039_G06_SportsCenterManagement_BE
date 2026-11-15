namespace SportsCenterManagement.BLL.Services;

public enum EnrollmentEntitlementVerdict
{
    Allowed,
    ClassLimitReached,
    SportLimitReached,
    SportNotInPackage
}

/// <summary>
/// Package allowance rule shared by the enrollment write path and the member-facing options list,
/// so what the UI offers can never drift from what <see cref="CoreFlowService.EnrollMemberAsync"/> accepts.
/// </summary>
public static class EnrollmentEntitlementRules
{
    /// <param name="used">Classes (with their sport) already consuming this subscription, distinct by class.</param>
    /// <param name="pickedSports">Sports locked in at purchase (SubscriptionSports); empty for legacy subscriptions.</param>
    /// <remarks>Sport rule mirrors SessionBookingService.EnsureEligibleAsync: all-access wins, then explicit picks, then the implicit count.</remarks>
    public static EnrollmentEntitlementVerdict Evaluate(
        int? maxClasses,
        int allowedSports,
        bool isAllAccess,
        IReadOnlyCollection<long> pickedSports,
        IReadOnlyCollection<(long ClassId, long SportId)> used,
        long classId,
        long sportId)
    {
        if (!isAllAccess && pickedSports.Count > 0 && !pickedSports.Contains(sportId))
            return EnrollmentEntitlementVerdict.SportNotInPackage;
        var classAlreadyUsed = used.Any(item => item.ClassId == classId);
        if (!classAlreadyUsed && maxClasses is int max && used.Count >= max)
            return EnrollmentEntitlementVerdict.ClassLimitReached;
        if (!isAllAccess && pickedSports.Count == 0 && !used.Any(item => item.SportId == sportId)
            && used.Select(item => item.SportId).Distinct().Count() >= allowedSports)
            return EnrollmentEntitlementVerdict.SportLimitReached;
        return EnrollmentEntitlementVerdict.Allowed;
    }
}

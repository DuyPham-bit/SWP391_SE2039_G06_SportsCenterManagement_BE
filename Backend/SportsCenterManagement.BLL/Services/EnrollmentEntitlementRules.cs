namespace SportsCenterManagement.BLL.Services;

public enum EnrollmentEntitlementVerdict
{
    Allowed,
    ClassLimitReached,
    SportLimitReached
}

/// <summary>
/// Package allowance rule shared by the enrollment write path and the member-facing options list,
/// so what the UI offers can never drift from what <see cref="CoreFlowService.EnrollMemberAsync"/> accepts.
/// </summary>
public static class EnrollmentEntitlementRules
{
    /// <param name="used">Classes (with their sport) already consuming this subscription, distinct by class.</param>
    public static EnrollmentEntitlementVerdict Evaluate(
        int? maxClasses,
        int allowedSports,
        IReadOnlyCollection<(long ClassId, long SportId)> used,
        long classId,
        long sportId)
    {
        var classAlreadyUsed = used.Any(item => item.ClassId == classId);
        var sportAlreadyUsed = used.Any(item => item.SportId == sportId);
        if (!classAlreadyUsed && maxClasses is int max && used.Count >= max)
            return EnrollmentEntitlementVerdict.ClassLimitReached;
        if (!sportAlreadyUsed && used.Select(item => item.SportId).Distinct().Count() >= allowedSports)
            return EnrollmentEntitlementVerdict.SportLimitReached;
        return EnrollmentEntitlementVerdict.Allowed;
    }
}

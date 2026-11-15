using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.Tests;

public class EnrollmentEntitlementRulesTests
{
    private const long Yoga = 1, Boxing = 2;

    [Fact]
    public void PickedSports_RejectsSportOutsideSelection_EvenWithSportAllowanceLeft()
    {
        var verdict = EnrollmentEntitlementRules.Evaluate(maxClasses: null, allowedSports: 2, isAllAccess: false,
            pickedSports: [Yoga], used: [], classId: 10, sportId: Boxing);

        Assert.Equal(EnrollmentEntitlementVerdict.SportNotInPackage, verdict);
    }

    [Fact]
    public void PickedSports_AllowsSelectedSport()
    {
        var verdict = EnrollmentEntitlementRules.Evaluate(null, 1, false, [Yoga], [(11, Yoga)], 10, Yoga);

        Assert.Equal(EnrollmentEntitlementVerdict.Allowed, verdict);
    }

    [Fact]
    public void AllAccess_IgnoresZeroSportAllowance()
    {
        var verdict = EnrollmentEntitlementRules.Evaluate(null, 0, true, [], [(11, Yoga)], 10, Boxing);

        Assert.Equal(EnrollmentEntitlementVerdict.Allowed, verdict);
    }

    [Fact]
    public void AllAccess_StillEnforcesClassLimit()
    {
        var verdict = EnrollmentEntitlementRules.Evaluate(1, 0, true, [], [(11, Yoga)], 10, Boxing);

        Assert.Equal(EnrollmentEntitlementVerdict.ClassLimitReached, verdict);
    }

    [Fact]
    public void LegacySubscription_KeepsImplicitSportCount()
    {
        var verdict = EnrollmentEntitlementRules.Evaluate(null, 1, false, [], [(11, Yoga)], 10, Boxing);

        Assert.Equal(EnrollmentEntitlementVerdict.SportLimitReached, verdict);
    }
}

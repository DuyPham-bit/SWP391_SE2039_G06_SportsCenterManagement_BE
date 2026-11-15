using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.Tests;

public class ClassScheduleButtonStateTests
{
    private static readonly MembershipEntitlements.SubscriptionEntitlement AllAccess = new(
        1, 1, "All", CenterId: 1, TierLevel: 1, IsAllAccess: true, AllowedSports: 0, CancelBeforeHours: 2,
        MaxFreezeDays: 0, StartDate: TestDb.Today.AddDays(-1), EndDate: TestDb.Today.AddDays(30),
        SelectedSportIds: new HashSet<long>(), UsedSportIds: new HashSet<long>());

    [Fact]
    public void SuspendedMember_GetsSuspendedInsteadOfCanBook()
    {
        var state = ClassScheduleQueryService.ResolveButtonState([AllAccess], alreadyBooked: false, started: false,
            TestDb.Today.AddDays(1), centerId: 1, sportId: 5, availableSeats: 3, openForBooking: true,
            bookingSuspended: true);

        Assert.Equal("SUSPENDED", state);
    }

    [Fact]
    public void SuspendedMember_StillSeesExistingBooking()
    {
        var state = ClassScheduleQueryService.ResolveButtonState([AllAccess], alreadyBooked: true, started: false,
            TestDb.Today.AddDays(1), 1, 5, 3, true, bookingSuspended: true);

        Assert.Equal("ALREADY_BOOKED", state);
    }
}

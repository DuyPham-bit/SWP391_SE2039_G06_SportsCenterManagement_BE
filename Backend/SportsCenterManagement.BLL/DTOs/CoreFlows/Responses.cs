namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public static class Responses
{
    public sealed record PendingMembershipResult(
        long SubscriptionId,
        long InvoiceId,
        string InvoiceNumber,
        decimal Amount);

    public sealed record RevenueSummary(
        long CenterId,
        DateOnly From,
        DateOnly To,
        decimal Gross,
        decimal Refunds,
        decimal Net);
}
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record RevenueSummary(
    long CenterId,
    DateOnly From,
    DateOnly To,
    decimal Gross,
    decimal Refunds,
    decimal Net);
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record RevenueReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    string TimeZone,
    string GroupBy,
    decimal Gross,
    decimal Refunds,
    decimal Net,
    IReadOnlyList<RevenuePeriod> Periods);

public sealed record RevenuePeriod(
    DateOnly PeriodStart,
    decimal Gross,
    decimal Refunds,
    decimal Net);

public sealed record SportRevenueReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    decimal Net,
    decimal AllocatedNet,
    decimal UnallocatedNet,
    IReadOnlyList<SportRevenueItem> Sports);

public sealed record SportRevenueItem(
    long? SportId,
    string SportName,
    decimal Gross,
    decimal Refunds,
    decimal Net,
    decimal Share);
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record PendingMembershipResult(
    long SubscriptionId,
    long InvoiceId,
    string InvoiceNumber,
    decimal Amount);
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record MembershipReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    string TimeZone,
    int TotalMembers,
    int ActiveMembers,
    int NewMembers,
    IReadOnlyList<MembershipReportPeriod> Periods,
    int ExpiredMembers = 0,
    int InactiveMembers = 0,
    IReadOnlyList<MembershipPackageStatsItem>? Packages = null,
    int MembersWithPackage = 0,
    int MembersWithoutPackage = 0);

public sealed record MembershipReportPeriod(DateOnly PeriodStart, int NewMembers);

public sealed record MembershipPackageStatsItem(
    long Id,
    string Name,
    decimal Price,
    int DurationDays,
    int ActiveSubscribers,
    int SoldInPeriod,
    decimal RevenueInPeriod);
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record ClassEnrollmentReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    string TimeZone,
    string GroupBy,
    IReadOnlyDictionary<string, int> CurrentStatusCounts,
    IReadOnlyList<ClassEnrollmentReportPeriod> Periods);

public sealed record ClassEnrollmentReportPeriod(
    DateOnly PeriodStart,
    IReadOnlyDictionary<string, int> StatusCounts);

public sealed record OperationsReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    int TotalStaff,
    int ActiveStaff,
    int ManagerCount,
    int CoachCount,
    int ReceptionistCount,
    int TotalClasses,
    int ActiveClasses,
    int TotalRooms,
    int ActiveRooms,
    int SessionsCount,
    int BookedSeats,
    int CapacitySeats,
    decimal OccupancyRate,
    IReadOnlyList<OperationalClassItem> Classes,
    IReadOnlyList<OperationalSportItem> Sports,
    IReadOnlyList<OperationalRoomItem> Rooms,
    IReadOnlyList<OperationalCoachItem>? Coaches = null);

public sealed record OperationalCoachItem(
    long Id,
    string FullName,
    string? CoachCode,
    string? Specialization,
    string Status,
    int ClassesCount,
    int SessionsCount,
    int Booked,
    int Capacity,
    decimal OccupancyRate);

public sealed record OperationalClassItem(
    long Id,
    string Name,
    string SportName,
    string? CoachName,
    string? RoomName,
    int SessionsCount,
    int Booked,
    int Capacity,
    decimal OccupancyRate);

public sealed record OperationalSportItem(
    long Id,
    string Name,
    int SessionsCount,
    int ClassesCount,
    int Booked,
    int Capacity,
    decimal OccupancyRate,
    decimal BookingShare);

public sealed record OperationalRoomItem(
    long Id,
    string Name,
    string? Type,
    string? Location,
    int UnitCapacity,
    int SessionsCount,
    int ClassesCount,
    int Booked,
    int Capacity,
    decimal OccupancyRate,
    IReadOnlyList<string> ClassNames);
}

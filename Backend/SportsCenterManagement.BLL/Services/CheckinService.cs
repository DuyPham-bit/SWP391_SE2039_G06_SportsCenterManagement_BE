using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Checkins;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CheckinService(IUnitOfWork unitOfWork) : ICheckinService
{
    private static readonly string[] CounterRoles =
    [
        "Admin",
        "Manager",
        "CenterManager",
        "Receptionist"
    ];

    public async Task<CheckinEligibilityResponse> GetEligibilityAsync(
        long staffUserId,
        long centerId,
        long? memberId,
        string? memberCode,
        CancellationToken cancellationToken = default)
    {
        await StaffAuthorization.RequireAsync(unitOfWork, staffUserId, centerId, CounterRoles, cancellationToken);
        var now = DateTime.UtcNow;
        return await BuildEligibilityAsync(centerId, memberId, memberCode, now, cancellationToken);
    }

    public async Task<CounterCheckinResponse> CheckInAsync(
        long staffUserId,
        long centerId,
        CounterCheckinRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        await StaffAuthorization.RequireAsync(unitOfWork, staffUserId, centerId, CounterRoles, cancellationToken);

        await using var transaction = await unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var now = DateTime.UtcNow;
        var eligibility = await BuildEligibilityAsync(
            centerId,
            request.MemberId,
            request.MemberCode,
            now,
            cancellationToken);

        if (eligibility.Member is null || eligibility.Subscription is null)
        {
            throw new InvalidOperationException(eligibility.Message);
        }

        if (eligibility.AlreadyCheckedInToday && eligibility.ExistingCheckinId.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);

            return new CounterCheckinResponse(
                Success: true,
                Message: "Member was already checked in for this business date.",
                CheckinId: eligibility.ExistingCheckinId.Value,
                CenterId: centerId,
                BusinessDate: eligibility.BusinessDate,
                CheckInTimeUtc: eligibility.ExistingCheckInTimeUtc ?? now,
                IsDuplicate: true,
                CheckedInBy: staffUserId,
                Member: eligibility.Member,
                Subscription: eligibility.Subscription);
        }

        if (!eligibility.Allowed)
        {
            throw new InvalidOperationException(eligibility.Message);
        }

        var checkin = new CenterCheckin
        {
            MemberId = eligibility.Member.MemberId,
            CenterId = centerId,
            CheckedInBy = staffUserId,
            CheckInTime = now,
            CreatedAt = now
        };
        await unitOfWork.Repository<CenterCheckin>().AddAsync(checkin, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            UserId = staffUserId,
            Action = "CounterCheckIn",
            EntityType = nameof(CenterCheckin),
            EntityId = checkin.Id,
            OldValues = null,
            NewValues = JsonSerializer.Serialize(new
            {
                checkin.Id,
                checkin.MemberId,
                checkin.CenterId,
                SubscriptionId = eligibility.Subscription.SubscriptionId,
                BusinessDate = eligibility.BusinessDate
            }),
            IpAddress = TrimToLength(ipAddress, 45),
            UserAgent = TrimToLength(userAgent, 500),
            CreatedAt = now
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CounterCheckinResponse(
            Success: true,
            Message: "Check-in completed successfully.",
            CheckinId: checkin.Id,
            CenterId: centerId,
            BusinessDate: eligibility.BusinessDate,
            CheckInTimeUtc: now,
            IsDuplicate: false,
            CheckedInBy: staffUserId,
            Member: eligibility.Member,
            Subscription: eligibility.Subscription);
    }

    public async Task<IReadOnlyList<CheckinListItemResponse>> GetDailyCheckinsAsync(
        long staffUserId,
        long centerId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        await StaffAuthorization.RequireAsync(unitOfWork, staffUserId, centerId, CounterRoles, cancellationToken);

        var startUtc = VietnamTime.ToUtc(businessDate);
        var endUtc = VietnamTime.ToUtc(businessDate.AddDays(1));

        return await (
            from checkin in unitOfWork.Context.CenterCheckins.AsNoTracking()
            join member in unitOfWork.Context.MemberProfiles.AsNoTracking()
                on checkin.MemberId equals member.Id
            where checkin.CenterId == centerId
                && checkin.CheckInTime >= startUtc
                && checkin.CheckInTime < endUtc
            orderby checkin.CheckInTime descending
            select new CheckinListItemResponse(
                checkin.Id,
                checkin.CenterId,
                businessDate,
                checkin.CheckInTime,
                checkin.CheckOutTime,
                member.Id,
                member.MemberCode,
                member.FullName,
                checkin.CheckedInBy))
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    private async Task<CheckinEligibilityResponse> BuildEligibilityAsync(
        long centerId,
        long? memberId,
        string? memberCode,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!memberId.HasValue && string.IsNullOrWhiteSpace(memberCode))
        {
            throw new InvalidOperationException("MemberId or MemberCode is required.");
        }

        var normalizedCode = memberCode?.Trim().ToUpperInvariant();
        var member = await (
            from profile in unitOfWork.Context.MemberProfiles.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on profile.UserId equals user.Id
            where (!memberId.HasValue || profile.Id == memberId.Value)
                && (string.IsNullOrEmpty(normalizedCode) || profile.MemberCode.ToUpper() == normalizedCode)
            select new
            {
                profile.Id,
                profile.MemberCode,
                profile.FullName,
                UserStatus = user.Status
            }).SingleOrDefaultAsync(cancellationToken);

        if (member is null || !member.UserStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException("Active member profile was not found.");
        }

        var businessDate = VietnamTime.GetDate(now);
        var subscription = await (
            from item in unitOfWork.Context.MemberSubscriptions.AsNoTracking()
            join package in unitOfWork.Context.MembershipPackages.AsNoTracking()
                on item.PackageId equals package.Id
            where item.MemberId == member.Id
                && package.CenterId == centerId
                && item.Status == "Active"
                && item.StartDate <= businessDate
                && item.EndDate >= businessDate
            orderby item.EndDate descending, item.Id descending
            select new CheckinSubscriptionSummary(
                item.Id,
                package.Id,
                package.Name,
                item.StartDate!.Value,
                item.EndDate!.Value))
            .FirstOrDefaultAsync(cancellationToken);

        var memberSummary = new CheckinMemberSummary(member.Id, member.MemberCode, member.FullName);
        var existing = await FindExistingCheckinAsync(member.Id, centerId, businessDate, cancellationToken);

        if (subscription is null)
        {
            return new CheckinEligibilityResponse(
                Allowed: false,
                Message: "Member does not have an active subscription for this center today.",
                BusinessDate: businessDate,
                Member: memberSummary,
                Subscription: null,
                AlreadyCheckedInToday: existing is not null,
                ExistingCheckinId: existing?.Id,
                ExistingCheckInTimeUtc: existing?.CheckInTime);
        }

        return new CheckinEligibilityResponse(
            Allowed: existing is null,
            Message: existing is null
                ? "Member is eligible for check-in."
                : "Member was already checked in for this business date.",
            BusinessDate: businessDate,
            Member: memberSummary,
            Subscription: subscription,
            AlreadyCheckedInToday: existing is not null,
            ExistingCheckinId: existing?.Id,
            ExistingCheckInTimeUtc: existing?.CheckInTime);
    }

    private async Task<CenterCheckin?> FindExistingCheckinAsync(
        long memberId,
        long centerId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var startUtc = VietnamTime.ToUtc(businessDate);
        var endUtc = VietnamTime.ToUtc(businessDate.AddDays(1));

        return await unitOfWork.Context.CenterCheckins
            .Where(item => item.MemberId == memberId
                && item.CenterId == centerId
                && item.CheckInTime >= startUtc
                && item.CheckInTime < endUtc)
            .OrderByDescending(item => item.CheckInTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Checkins;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CheckinService(IUnitOfWork unitOfWork) : ICheckinService
{
    public async Task<CheckinResponse> CheckInMemberAsync(
        long actorUserId,
        long centerId,
        CheckinRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCenterStaffAsync(actorUserId, centerId, cancellationToken);

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var searchId = request.MemberId;
        var term = request.Identifier?.Trim();

        if (!searchId.HasValue && string.IsNullOrWhiteSpace(term))
        {
            throw new InvalidOperationException("Vui lòng nhập mã thẻ hội viên, số điện thoại hoặc email.") { Data = { ["StatusCode"] = 400 } };
        }

        // 1. Tìm hồ sơ hội viên
        MemberProfile? member = null;
        if (searchId.HasValue)
        {
            member = await unitOfWork.Context.MemberProfiles.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == searchId.Value && m.CenterId == centerId, cancellationToken);
        }

        if (!searchId.HasValue && !string.IsNullOrWhiteSpace(term))
        {
            member = await (
                from m in unitOfWork.Context.MemberProfiles.AsNoTracking()
                join u in unitOfWork.Context.Users.AsNoTracking() on m.UserId equals u.Id
                where m.CenterId == centerId && (
                    m.MemberCode == term
                    || u.Phone == term
                    || u.Email == term
                    || u.Username == term
                )
                select m
            ).FirstOrDefaultAsync(cancellationToken);
        }

        if (member == null)
        {
            throw new InvalidOperationException($"Không tìm thấy hội viên nào có mã/SĐT/Email: \"{term ?? searchId?.ToString()}\".") { Data = { ["StatusCode"] = 404 } };
        }

        // 2. Kiểm tra tài khoản người dùng có Active không
        var user = await unitOfWork.Context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == member.UserId, cancellationToken);
        if (user == null || user.Status != "Active" || (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow))
        {
            throw new InvalidOperationException($"Tài khoản hội viên {member.FullName} ({member.MemberCode}) đang bị khóa.")
            {
                Data =
                {
                    ["StatusCode"] = 400
                }
            };
        }

        // 3. Kiểm tra gói tập còn hiệu lực không
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var activeSub = await (
            from s in unitOfWork.Context.MemberSubscriptions.AsNoTracking()
            join pkg in unitOfWork.Context.MembershipPackages.AsNoTracking() on s.PackageId equals pkg.Id
            where s.MemberId == member.Id && pkg.CenterId == centerId && s.Status == "Active"
                && s.StartDate.HasValue && s.StartDate <= today && s.EndDate.HasValue && s.EndDate >= today
            orderby s.EndDate descending
            select new { s.Id, s.PackageId, PackageName = pkg.Name }
        ).FirstOrDefaultAsync(cancellationToken);

        if (activeSub == null)
        {
            throw new InvalidOperationException($"Hội viên {member.FullName} ({member.MemberCode}) chưa có gói tập hợp lệ hoặc gói đã hết hạn.")
            {
                Data =
                {
                    ["StatusCode"] = 400
                }
            };
        }

        // 3.1. Chặn check-in trùng lặp trong vòng 15 phút (RE-05)
        var now = DateTime.UtcNow;
        var recentCutoff = now.AddMinutes(-15);
        var recentCheckin = await unitOfWork.Context.CenterCheckins.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && c.MemberId == member.Id && c.CheckInTime >= recentCutoff, cancellationToken);
        if (recentCheckin != null)
        {
            var localRecentTime = recentCheckin.CheckInTime.AddHours(7).ToString("HH:mm");
            throw new InvalidOperationException($"Hội viên {member.FullName} ({member.MemberCode}) vừa được check-in lúc {localRecentTime}.")
            {
                Data =
                {
                    ["StatusCode"] = 409
                }
            };
        }

        // 4. Tạo bản ghi check-in mới
        var checkin = new CenterCheckin
        {
            CenterId = centerId,
            MemberId = member.Id,
            CheckedInBy = actorUserId,
            CheckInTime = now,
            CreatedAt = now
        };

        await unitOfWork.Context.CenterCheckins.AddAsync(checkin, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        unitOfWork.Context.AuditLogs.Add(new AuditLog { UserId = actorUserId, CenterId = centerId,
            Action = "Checkin.Recorded", EntityType = "CenterCheckin", EntityId = checkin.Id, CreatedAt = now });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // 5. Lấy tên nhân viên lễ tân thực hiện
        var staffProfile = await unitOfWork.Context.StaffProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == actorUserId, cancellationToken);
        var staffUser = await unitOfWork.Context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == actorUserId, cancellationToken);
        var receptionistName = staffProfile?.FullName ?? staffUser?.Username ?? "Lễ tân";

        // Định dạng thời gian check-in: yyyy-MM-dd HH:mm:ss (theo giờ địa phương +7)
        var localTime = now.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss");

        return new CheckinResponse(
            checkin.Id,
            member.Id,
            member.FullName,
            member.MemberCode,
            activeSub.PackageName,
            localTime,
            receptionistName
        );
    }

    public async Task<IReadOnlyList<CheckinResponse>> GetTodayCheckinsAsync(
        long actorUserId,
        long centerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCenterStaffAsync(actorUserId, centerId, cancellationToken);

        var todayUtc = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
        var tomorrowUtc = todayUtc.AddDays(1);
        var checkins = await (
            from c in unitOfWork.Context.CenterCheckins.AsNoTracking()
            join m in unitOfWork.Context.MemberProfiles.AsNoTracking() on c.MemberId equals m.Id
            join staff in unitOfWork.Context.StaffProfiles.AsNoTracking() on c.CheckedInBy equals staff.UserId into staffJoin
            from staff in staffJoin.DefaultIfEmpty()
            where c.CenterId == centerId && c.CheckInTime >= todayUtc && c.CheckInTime < tomorrowUtc
            orderby c.CheckInTime descending, c.Id descending
            select new
            {
                c.Id,
                c.MemberId,
                m.FullName,
                m.MemberCode,
                c.CheckInTime,
                ReceptionistName = staff != null ? staff.FullName : "Lễ tân"
            }
        ).Take(100).ToListAsync(cancellationToken);

        if (checkins.Count == 0)
        {
            return Array.Empty<CheckinResponse>();
        }

        var memberIds = checkins.Select(c => c.MemberId).Distinct().ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var memberSubsList = await (
            from s in unitOfWork.Context.MemberSubscriptions.AsNoTracking()
            join pkg in unitOfWork.Context.MembershipPackages.AsNoTracking() on s.PackageId equals pkg.Id
            where memberIds.Contains(s.MemberId) && pkg.CenterId == centerId && s.Status == "Active"
                && s.StartDate.HasValue && s.StartDate <= today && s.EndDate.HasValue && s.EndDate >= today
            orderby s.EndDate descending
            select new { s.MemberId, PackageName = pkg.Name }
        ).ToListAsync(cancellationToken);

        var memberPackages = memberSubsList
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.First().PackageName);

        return checkins.Select(c => new CheckinResponse(
            c.Id,
            c.MemberId,
            c.FullName,
            c.MemberCode,
            memberPackages.GetValueOrDefault(c.MemberId, "Gói thành viên"),
            c.CheckInTime.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"),
            c.ReceptionistName
        )).ToList();
    }

    public async Task<CheckinSummary> GetTodaySummaryAsync(long actorUserId, long centerId, CancellationToken cancellationToken = default)
    {
        await EnsureCenterStaffAsync(actorUserId, centerId, cancellationToken);
        var date = DateTime.UtcNow.AddHours(7).Date;
        var start = date.AddHours(-7);
        var end = start.AddDays(1);
        var rows = unitOfWork.Context.CenterCheckins.AsNoTracking()
            .Where(c => c.CenterId == centerId && c.CheckInTime >= start && c.CheckInTime < end);
        return new CheckinSummary(DateOnly.FromDateTime(date), await rows.CountAsync(cancellationToken),
            await rows.Select(c => c.MemberId).Distinct().CountAsync(cancellationToken));
    }

    private async Task EnsureCenterStaffAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new InvalidOperationException("Tài khoản không tồn tại hoặc phiên đăng nhập hết hạn.") { Data = { ["StatusCode"] = 403 } };
        if (user.Status != "Active" || user.LockedUntil > DateTime.UtcNow)
            throw new InvalidOperationException("Tài khoản không hoạt động.") { Data = { ["StatusCode"] = 403 } };

        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken);
        if (role?.Name == RoleNames.Admin)
        {
            return;
        }

        var centerIsActive = await unitOfWork.Repository<Center>()
            .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken);
        if (!centerIsActive)
        {
            throw new InvalidOperationException("Cơ sở trung tâm thể thao không hoạt động.") { Data = { ["StatusCode"] = 400 } };
        }

        var assigned = await unitOfWork.Repository<StaffProfile>()
            .AnyAsync(profile => profile.UserId == actorUserId
                                 && profile.CenterId == centerId
                                 && profile.Status == "Active", cancellationToken);
        if (!assigned)
        {
            throw new InvalidOperationException("Tài khoản nhân sự không được phân công tại cơ sở này.") { Data = { ["StatusCode"] = 403 } };
        }
    }
}

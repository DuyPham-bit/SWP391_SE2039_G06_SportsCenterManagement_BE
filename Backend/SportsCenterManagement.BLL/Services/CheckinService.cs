using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Checkins;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Interfaces;
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

        var searchId = request.MemberId;
        var term = request.Identifier?.Trim();

        if (!searchId.HasValue && string.IsNullOrWhiteSpace(term))
        {
            throw FlowException.BadRequest("Vui lòng nhập mã thẻ hội viên, số điện thoại hoặc email.");
        }

        // 1. Tìm hồ sơ hội viên
        MemberProfile? member = null;
        if (searchId.HasValue)
        {
            member = await unitOfWork.Context.MemberProfiles.AsNoTracking()
                .FirstOrDefaultAsync(m => (m.Id == searchId.Value || m.UserId == searchId.Value) && m.CenterId == centerId, cancellationToken);
        }

        if (member == null && !string.IsNullOrWhiteSpace(term))
        {
            var isNumeric = long.TryParse(term, out var numericId);
            member = await (
                from m in unitOfWork.Context.MemberProfiles.AsNoTracking()
                join u in unitOfWork.Context.Users.AsNoTracking() on m.UserId equals u.Id
                where m.CenterId == centerId && (
                    m.MemberCode == term
                    || (isNumeric && (m.Id == numericId || m.UserId == numericId))
                    || u.Phone == term
                    || u.Email == term
                    || u.Username == term
                )
                select m
            ).FirstOrDefaultAsync(cancellationToken);
        }

        if (member == null)
        {
            throw FlowException.NotFound($"Không tìm thấy hội viên nào có mã/SĐT/Email: \"{term ?? searchId?.ToString()}\".");
        }

        // 2. Kiểm tra tài khoản người dùng có Active không
        var user = await unitOfWork.Context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == member.UserId, cancellationToken);
        if (user == null || user.Status != "Active" || (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow))
        {
            throw FlowException.BadRequest($"Tài khoản hội viên {member.FullName} ({member.MemberCode}) đang bị khóa hoặc không hoạt động.");
        }

        // 3. Kiểm tra gói tập còn hiệu lực không
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeSub = await (
            from s in unitOfWork.Context.MemberSubscriptions.AsNoTracking()
            join pkg in unitOfWork.Context.MembershipPackages.AsNoTracking() on s.PackageId equals pkg.Id
            where s.MemberId == member.Id && s.Status == "Active" && (!s.EndDate.HasValue || s.EndDate.Value >= today)
            orderby s.EndDate descending
            select new { s.Id, s.PackageId, PackageName = pkg.Name }
        ).FirstOrDefaultAsync(cancellationToken);

        if (activeSub == null)
        {
            throw FlowException.BadRequest($"Hội viên {member.FullName} ({member.MemberCode}) chưa có gói tập hợp lệ hoặc gói đã hết hạn. Vui lòng gia hạn gói tập tại quầy.");
        }

        // 4. Tạo bản ghi check-in mới
        var now = DateTime.UtcNow;
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

        // Lấy các lượt checkin trong ngày hôm nay (UTC)
        var todayUtc = DateTime.UtcNow.Date;
        var checkins = await (
            from c in unitOfWork.Context.CenterCheckins.AsNoTracking()
            join m in unitOfWork.Context.MemberProfiles.AsNoTracking() on c.MemberId equals m.Id
            join staff in unitOfWork.Context.StaffProfiles.AsNoTracking() on c.CheckedInBy equals staff.UserId into staffJoin
            from staff in staffJoin.DefaultIfEmpty()
            where c.CenterId == centerId && c.CheckInTime >= todayUtc
            orderby c.CheckInTime descending
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var memberSubsList = await (
            from s in unitOfWork.Context.MemberSubscriptions.AsNoTracking()
            join pkg in unitOfWork.Context.MembershipPackages.AsNoTracking() on s.PackageId equals pkg.Id
            where memberIds.Contains(s.MemberId) && s.Status == "Active" && (!s.EndDate.HasValue || s.EndDate.Value >= today)
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

    private async Task EnsureCenterStaffAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw FlowException.Forbidden("Tài khoản không tồn tại hoặc phiên đăng nhập hết hạn.");

        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken);
        if (role?.Name is "Admin" or "SystemAdmin")
        {
            return;
        }

        var centerIsActive = await unitOfWork.Repository<Center>()
            .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken);
        if (!centerIsActive)
        {
            throw FlowException.BadRequest("Cơ sở trung tâm thể thao không hoạt động.");
        }

        var assigned = await unitOfWork.Repository<StaffProfile>()
            .AnyAsync(profile => profile.UserId == actorUserId
                                 && profile.CenterId == centerId
                                 && profile.Status == "Active", cancellationToken);
        if (!assigned)
        {
            throw FlowException.Forbidden("Tài khoản nhân sự không được phân công tại cơ sở này.");
        }
    }
}

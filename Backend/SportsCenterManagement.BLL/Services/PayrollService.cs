using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Payroll;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class PayrollService(IUnitOfWork unitOfWork)
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PayrollRow> GetMine(long userId, int year, int month, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        var report = await BuildReport(null, year, month, ct, userId);
        return report.Employees.SingleOrDefault()
            ?? throw BusinessException.NotFound("Tài khoản chưa có hồ sơ nhân sự để xem lương.");
    }

    public async Task<PayrollReport> GetReport(long actorId, int year, int month, CancellationToken ct)
    {
        ValidatePeriod(year, month);
        var actor = await (from user in _unitOfWork.Context.Users
                           join role in _unitOfWork.Context.Roles on user.RoleId equals role.Id
                           where user.Id == actorId
                           select new { Role = role.Name }).SingleOrDefaultAsync(ct)
            ?? throw BusinessException.Forbidden("Tài khoản không hợp lệ.");
        long? centerId = null;
        if (!actor.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            centerId = await _unitOfWork.Context.StaffProfiles.Where(profile => profile.UserId == actorId && profile.Status == "Active")
                .Select(profile => (long?)profile.CenterId).SingleOrDefaultAsync(ct)
                ?? throw BusinessException.Forbidden("Quản lý chưa được gán trung tâm.");
            if (!actor.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
                throw BusinessException.Forbidden("Bạn không có quyền xem báo cáo lương.");
        }
        return await BuildReport(centerId, year, month, ct);
    }

    public async Task<long> SaveStaffPolicy(long actorId, SaveStaffCompensationRequest request, CancellationToken ct)
    {
        if (request.EffectiveTo.HasValue && request.EffectiveTo < request.EffectiveFrom)
            throw BusinessException.BadRequest("Khoảng hiệu lực chính sách lương không hợp lệ.");
        var effectiveFrom = request.EffectiveFrom == default
            ? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
            : request.EffectiveFrom;

        var actor = await (from user in _unitOfWork.Context.Users
                           join role in _unitOfWork.Context.Roles on user.RoleId equals role.Id
                           where user.Id == actorId select role.Name).SingleOrDefaultAsync(ct)
            ?? throw BusinessException.Forbidden("Tài khoản không hợp lệ.");
        var target = await (from user in _unitOfWork.Context.Users
                            join role in _unitOfWork.Context.Roles on user.RoleId equals role.Id
                            join staff in _unitOfWork.Context.StaffProfiles on user.Id equals staff.UserId into staffRows
                            from staff in staffRows.DefaultIfEmpty()
                            where user.Id == request.UserId
                            select new { Role = role.Name, CenterId = staff == null ? (long?)null : staff.CenterId }).SingleOrDefaultAsync(ct)
            ?? throw BusinessException.NotFound("Không tìm thấy nhân sự.");

        var coach = await _unitOfWork.Context.CoachProfiles.SingleOrDefaultAsync(c => c.UserId == request.UserId, ct);
        var targetCenterId = target.CenterId ?? coach?.CenterId;

        if (!actor.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.Equals("Manager", StringComparison.OrdinalIgnoreCase)) throw BusinessException.Forbidden("Bạn không có quyền cập nhật lương.");
            var centerId = await _unitOfWork.Context.StaffProfiles.Where(profile => profile.UserId == actorId)
                .Select(profile => (long?)profile.CenterId).SingleOrDefaultAsync(ct);
            if (!centerId.HasValue || targetCenterId != centerId) throw BusinessException.Forbidden("Chỉ được cập nhật nhân sự trong trung tâm của bạn.");
        }

        if (target.Role.Equals("Coach", StringComparison.OrdinalIgnoreCase) && coach != null)
        {
            var activeCoachPolicies = await _unitOfWork.Context.CoachCompensationPolicies
                .Where(policy => policy.CoachId == coach.Id && policy.Status == "Active").ToListAsync(ct);
            foreach (var item in activeCoachPolicies) item.Status = "Archived";
            var coachPolicy = new CoachCompensationPolicy
            {
                CoachId = coach.Id,
                MonthlyBaseSalary = request.MonthlyBaseSalary,
                CommissionPerSession = request.CommissionPerSession ?? 0m,
                TargetSessions = request.TargetSessions ?? 0,
                EffectiveFrom = effectiveFrom,
                EffectiveTo = request.EffectiveTo,
                Status = "Active",
                UpdatedBy = actorId,
                UpdatedAt = DateTime.UtcNow
            };
            _unitOfWork.Context.CoachCompensationPolicies.Add(coachPolicy);
            await _unitOfWork.SaveChangesAsync(ct);
            return coachPolicy.Id;
        }

        var active = await _unitOfWork.Context.StaffCompensationPolicies
            .Where(policy => policy.UserId == request.UserId && policy.Status == "Active").ToListAsync(ct);
        foreach (var policy in active) policy.Status = "Archived";
        var row = new StaffCompensationPolicy
        {
            UserId = request.UserId,
            CenterId = targetCenterId,
            MonthlyBaseSalary = request.MonthlyBaseSalary,
            MonthlyAllowance = request.MonthlyAllowance,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = "Active",
            UpdatedBy = actorId,
            UpdatedAt = DateTime.UtcNow
        };
        _unitOfWork.Context.StaffCompensationPolicies.Add(row);
        await _unitOfWork.SaveChangesAsync(ct);
        return row.Id;
    }

    private async Task<PayrollReport> BuildReport(long? centerId, int year, int month, CancellationToken ct, long? onlyUserId = null)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        // A coach is part of the payroll roster when the account is active and either
        // the coach profile or its staff employment profile is active. Older records
        // can have a stale coach-profile status even though the staff assignment is active.
        var coachQuery = _unitOfWork.Context.CoachProfiles.AsNoTracking().Where(coach =>
            _unitOfWork.Context.Users.Any(user => user.Id == coach.UserId && user.Status == "Active") &&
            (coach.Status == "Active" || _unitOfWork.Context.StaffProfiles.Any(staff =>
                staff.UserId == coach.UserId && staff.CenterId == coach.CenterId && staff.Status == "Active")));
        if (centerId.HasValue) coachQuery = coachQuery.Where(coach => coach.CenterId == centerId.Value);
        if (onlyUserId.HasValue) coachQuery = coachQuery.Where(coach => coach.UserId == onlyUserId.Value);
        var coaches = await coachQuery.ToListAsync(ct);
        var result = new List<PayrollRow>();
        foreach (var coach in coaches)
        {
            var policy = await _unitOfWork.Context.CoachCompensationPolicies.AsNoTracking()
                .Where(item => item.CoachId == coach.Id && item.Status == "Active" && item.EffectiveFrom < to &&
                    (!item.EffectiveTo.HasValue || item.EffectiveTo >= from))
                .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
            var classIds = _unitOfWork.Context.ClassCoaches.Where(item => item.CoachId == coach.Id).Select(item => item.ClassId);
            var sessions = await _unitOfWork.Context.ClassSessions.CountAsync(session => classIds.Contains(session.ClassId) &&
                (session.CoachId == null || session.CoachId == coach.Id) && session.SessionDate >= from && session.SessionDate < to &&
                session.SessionStatus == "Completed", ct);
            var sessionBonus = sessions * (policy?.CommissionPerSession ?? 0m);
            var centerName = await _unitOfWork.Context.Centers.Where(item => item.Id == coach.CenterId).Select(item => item.Name).SingleOrDefaultAsync(ct);
            result.Add(new PayrollRow(coach.UserId, coach.CoachCode, coach.FullName, "Coach", coach.CenterId, centerName,
                policy?.MonthlyBaseSalary ?? 0m, 0m, sessions, sessionBonus,
                (policy?.MonthlyBaseSalary ?? 0m) + sessionBonus, policy is not null,
                policy?.CommissionPerSession, policy?.TargetSessions,
                policy?.EffectiveFrom, policy?.EffectiveTo));
        }

        var staffQuery = from staff in _unitOfWork.Context.StaffProfiles.AsNoTracking()
                         join user in _unitOfWork.Context.Users.AsNoTracking() on staff.UserId equals user.Id
                         join role in _unitOfWork.Context.Roles.AsNoTracking() on user.RoleId equals role.Id
                         join center in _unitOfWork.Context.Centers.AsNoTracking() on staff.CenterId equals center.Id
                         where staff.Status == "Active" && user.Status == "Active" &&
                            (role.Name != "Coach" || !_unitOfWork.Context.CoachProfiles.Any(coach => coach.UserId == user.Id))
                         select new { staff.UserId, staff.StaffCode, staff.FullName, Role = role.Name, staff.CenterId, CenterName = center.Name };
        if (centerId.HasValue) staffQuery = staffQuery.Where(item => item.CenterId == centerId.Value);
        if (onlyUserId.HasValue) staffQuery = staffQuery.Where(item => item.UserId == onlyUserId.Value);
        var staffRows = await staffQuery.ToListAsync(ct);
        foreach (var staff in staffRows)
        {
            var policy = await _unitOfWork.Context.StaffCompensationPolicies.AsNoTracking()
                .Where(item => item.UserId == staff.UserId && item.Status == "Active" && item.EffectiveFrom < to &&
                    (!item.EffectiveTo.HasValue || item.EffectiveTo >= from))
                .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
            result.Add(new PayrollRow(staff.UserId, staff.StaffCode, staff.FullName, staff.Role, staff.CenterId, staff.CenterName,
                policy?.MonthlyBaseSalary ?? 0m, policy?.MonthlyAllowance ?? 0m, 0, 0m,
                (policy?.MonthlyBaseSalary ?? 0m) + (policy?.MonthlyAllowance ?? 0m), policy is not null,
                null, null, policy?.EffectiveFrom, policy?.EffectiveTo));
        }
        if (!centerId.HasValue)
        {
            var adminQuery = from user in _unitOfWork.Context.Users.AsNoTracking()
                             join role in _unitOfWork.Context.Roles on user.RoleId equals role.Id
                             where role.Name == "Admin" && user.Status == "Active"
                             select new { user.Id, user.Username };
            if (onlyUserId.HasValue) adminQuery = adminQuery.Where(item => item.Id == onlyUserId.Value);
            foreach (var admin in await adminQuery.ToListAsync(ct))
            {
                var policy = await _unitOfWork.Context.StaffCompensationPolicies.AsNoTracking()
                    .Where(item => item.UserId == admin.Id && item.Status == "Active" && item.EffectiveFrom < to &&
                        (!item.EffectiveTo.HasValue || item.EffectiveTo >= from))
                    .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
                result.Add(new PayrollRow(admin.Id, $"ADMIN-{admin.Id}", admin.Username, "Admin", null, null,
                    policy?.MonthlyBaseSalary ?? 0m, policy?.MonthlyAllowance ?? 0m, 0, 0m,
                    (policy?.MonthlyBaseSalary ?? 0m) + (policy?.MonthlyAllowance ?? 0m), policy is not null,
                    null, null, policy?.EffectiveFrom, policy?.EffectiveTo));
            }
        }
        result = result.GroupBy(item => item.UserId).Select(group => group.First()).OrderBy(item => item.FullName).ToList();
        return new PayrollReport(year, month, result.Sum(item => item.BaseSalary),
            result.Sum(item => item.Allowance + item.SessionBonus), result.Sum(item => item.EstimatedTotal), result);
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12) throw BusinessException.BadRequest("Kỳ lương không hợp lệ.");
    }
}

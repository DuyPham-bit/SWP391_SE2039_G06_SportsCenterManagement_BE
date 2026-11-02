using SportsCenterManagement.BLL.Common;
using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StaffRequests = SportsCenterManagement.BLL.DTOs.Staff.Requests;
using StaffResponses = SportsCenterManagement.BLL.DTOs.Staff.Responses;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class StaffManagementService(IUnitOfWork unitOfWork) : IStaffManagementService
{
    public async Task<StaffResponses.CenterStaffResponse[]> SearchAtCenterAsync(
        long actorUserId,
        long centerId,
        string? query,
        int page,
        int pageSize,
        string sortBy,
        string sortDirection,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanManageStaffAsync(actorUserId, centerId, cancellationToken, allowReadOnlyManager: true);
        var normalizedSort = sortBy.Trim().ToLowerInvariant();
        var normalizedDirection = sortDirection.Trim().ToLowerInvariant();
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100
            || query?.Trim().Length > 100
            || normalizedSort is not ("default" or "givenname" or "role")
            || normalizedDirection is not ("asc" or "desc"))
        {
            throw new ValidationException("Page cần từ 1; pageSize từ 1 đến 100; từ khóa tối đa 100 ký tự; kiểu sắp xếp không hợp lệ.");
        }

        var term = query?.Trim();
        var matches =
            from staff in unitOfWork.Context.StaffProfiles.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on staff.UserId equals user.Id
            join role in unitOfWork.Context.Roles.AsNoTracking() on user.RoleId equals role.Id
            join coachProfile in unitOfWork.Context.CoachProfiles.AsNoTracking()
                on user.Id equals coachProfile.UserId into coachProfiles
            from coach in coachProfiles.DefaultIfEmpty()
            where staff.CenterId == centerId
                  && (term == null || term == string.Empty
                      || staff.StaffCode.Contains(term)
                      || staff.FullName.Contains(term)
                      || user.Username.Contains(term)
                      || user.Email.Contains(term))
            let normalizedName = staff.FullName.Trim()
            let reversedName = SportsCenterDbContext.Reverse(normalizedName)
            let separatorIndex = reversedName.IndexOf(" ")
            let givenName = separatorIndex < 0
                ? normalizedName
                : SportsCenterDbContext.Reverse(reversedName.Substring(0, separatorIndex))
            select new
            {
                StatusOrder = user.Status == "Active" ? 0 : 1,
                GivenName = givenName,
                Staff = staff,
                User = user,
                RoleName = role.Name,
                Coach = coach
            };
        var ordered = normalizedSort switch
        {
            "givenname" when normalizedDirection == "desc" => matches.OrderBy(item => item.StatusOrder)
                .ThenByDescending(item => item.GivenName).ThenByDescending(item => item.Staff.FullName).ThenBy(item => item.Staff.Id),
            "givenname" => matches.OrderBy(item => item.StatusOrder)
                .ThenBy(item => item.GivenName).ThenBy(item => item.Staff.FullName).ThenBy(item => item.Staff.Id),
            "role" when normalizedDirection == "desc" => matches.OrderBy(item => item.StatusOrder)
                .ThenByDescending(item => item.RoleName).ThenBy(item => item.GivenName).ThenBy(item => item.Staff.Id),
            "role" => matches.OrderBy(item => item.StatusOrder)
                .ThenBy(item => item.RoleName).ThenBy(item => item.GivenName).ThenBy(item => item.Staff.Id),
            _ => matches.OrderBy(item => item.StatusOrder).ThenBy(item => item.Staff.FullName).ThenBy(item => item.Staff.Id)
        };
        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var coachIds = rows.Where(row => row.Coach is not null).Select(row => row.Coach!.Id).ToArray();
        var sportIdsByCoach = await unitOfWork.Context.CoachSports.AsNoTracking()
            .Where(item => coachIds.Contains(item.CoachId))
            .GroupBy(item => item.CoachId)
            .ToDictionaryAsync(group => group.Key, group => (IReadOnlyList<long>)group.Select(item => item.SportId).ToArray(), cancellationToken);
        var activeClassCountsByCoach = await (
                from assignment in unitOfWork.Context.ClassCoaches.AsNoTracking()
                join cls in unitOfWork.Context.Classes.AsNoTracking() on assignment.ClassId equals cls.Id
                where coachIds.Contains(assignment.CoachId)
                      && cls.CenterId == centerId
                      && cls.Status != FlowStatuses.ClassCancelled
                      && cls.Status != FlowStatuses.ClassCompleted
                group assignment by assignment.CoachId into assignments
                select new { CoachId = assignments.Key, Count = assignments.Select(item => item.ClassId).Distinct().Count() })
            .ToDictionaryAsync(item => item.CoachId, item => item.Count, cancellationToken);
        return rows.Select(row => Map(row.Staff, row.User, row.RoleName, row.Coach,
            row.Coach is not null && sportIdsByCoach.TryGetValue(row.Coach.Id, out var ids) ? ids : [],
            row.Coach is null ? null : activeClassCountsByCoach.GetValueOrDefault(row.Coach.Id))).ToArray();
    }

    public async Task<StaffResponses.CenterStaffResponse> CreateAtCenterAsync(
        long actorUserId,
        long centerId,
        StaffRequests.CreateCenterStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCredentials(request.Username, request.Email, request.Password, request.Phone);
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException("Họ tên không được để trống.");
        }
        if (request.HireDate > VietnamTime.Today)
        {
            throw new ValidationException("Ngày nhận việc không thể nằm trong tương lai.");
        }

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await EnsureCanManageStaffAsync(actorUserId, centerId, cancellationToken);
        var sports = await ResolveCoachSportsAsync(centerId, request.RoleName, request.SportIds, cancellationToken);
        var actor = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var actorRole = await unitOfWork.Repository<Role>().GetByIdAsync(actor.RoleId, cancellationToken);
        if (request.RoleName == RoleNames.Admin ||
            (request.RoleName == RoleNames.Manager && actorRole?.Name != RoleNames.Admin) ||
            (actorRole?.Name == RoleNames.Manager && request.RoleName is not (RoleNames.Coach or RoleNames.Receptionist)))
        {
            throw new UnauthorizedAccessException("Manager chỉ được tạo Coach hoặc Receptionist tại trung tâm mình quản lý.");
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = PhoneNumberNormalization.Normalize(request.Phone);
        var existingUser = await unitOfWork.Context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Username == normalizedUsername
                                                             || user.Email == normalizedEmail
                                                             || (normalizedPhone != null && user.Phone == normalizedPhone), cancellationToken);
        if (existingUser != null)
        {
            var duplicateField = existingUser.Email == normalizedEmail ? "Email" : (normalizedPhone != null && existingUser.Phone == normalizedPhone ? "Số điện thoại" : "Tên đăng nhập");
            throw new InvalidOperationException($"Tạo hồ sơ thất bại: {duplicateField} đã được sử dụng làm định danh trong hệ thống. Vui lòng kiểm tra lại hoặc mở hồ sơ hiện có.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "CM-01",
                    ["ProposedHandling"] = "Kiểm tra trùng trước khi lưu; thông báo rõ trường bị trùng và cho người có quyền mở hồ sơ hiện có."
                }
            };
        }

        var role = await unitOfWork.Context.Roles.SingleOrDefaultAsync(
            item => item.Name == request.RoleName, cancellationToken)
            ?? throw new InvalidOperationException("Role chưa được cấu hình trong hệ thống.");
        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            PasswordHash = PasswordHashing.Hash(request.Password),
            Phone = normalizedPhone,
            RoleId = role.Id,
            Status = "Active",
            CreatedAt = now
        };
        unitOfWork.Context.Users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var staff = new StaffProfile
        {
            UserId = user.Id,
            CenterId = centerId,
            StaffCode = MakeCode("ST"),
            FullName = request.FullName.Trim(),
            Position = request.RoleName,
            HireDate = request.HireDate,
            Status = "Active",
            CreatedAt = now
        };
        unitOfWork.Context.StaffProfiles.Add(staff);
        CoachProfile? coach = null;
        if (request.RoleName == RoleNames.Coach)
        {
            coach = new CoachProfile
            {
                UserId = user.Id,
                CenterId = centerId,
                CoachCode = MakeCode("CO"),
                FullName = staff.FullName,
                Specialization = Normalize(request.Specialization),
                Certification = Normalize(request.Certification),
                ExperienceYears = request.ExperienceYears,
                Bio = Normalize(request.Bio),
                HireDate = request.HireDate,
                Status = "Active",
                CreatedAt = now
            };
            unitOfWork.Context.CoachProfiles.Add(coach);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (coach is not null) SetCoachSports(coach, sports);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "staff.created", "User", user.Id,
            newValues: new { Role = request.RoleName, staff.StaffCode, staff.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(staff, user, role.Name, coach, sports.Select(item => item.Id).ToArray());
    }

    public async Task<StaffResponses.CenterStaffResponse> AppointAtCenterAsync(
        long actorUserId, long centerId, StaffRequests.AppointCenterStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RoleName is not (RoleNames.Coach or RoleNames.Receptionist) ||
            request.HireDate > VietnamTime.Today)
            throw new ValidationException("Vai trò hoặc ngày nhận việc không hợp lệ.");
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await EnsureCanManageStaffAsync(actorUserId, centerId, cancellationToken);
        var sports = await ResolveCoachSportsAsync(centerId, request.RoleName, request.SportIds, cancellationToken);
        var member = await unitOfWork.Context.MemberProfiles.SingleOrDefaultAsync(
            m => m.Id == request.MemberId && m.CenterId == centerId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hội viên trong trung tâm này.");
        var user = await unitOfWork.Context.Users.SingleAsync(u => u.Id == member.UserId, cancellationToken);
        var previousRole = await unitOfWork.Context.Roles.SingleAsync(r => r.Id == user.RoleId, cancellationToken);
        if (previousRole.Name != RoleNames.Member || user.Status != "Active" || user.LockedUntil > DateTime.UtcNow ||
            await unitOfWork.Context.StaffProfiles.AnyAsync(s => s.UserId == user.Id, cancellationToken) ||
            await unitOfWork.Context.CoachProfiles.AnyAsync(c => c.UserId == user.Id, cancellationToken))
            throw new InvalidOperationException("Chỉ bổ nhiệm hội viên đang hoạt động và chưa có hồ sơ nhân viên.");
        var role = await unitOfWork.Context.Roles.SingleAsync(r => r.Name == request.RoleName, cancellationToken);
        var now = DateTime.UtcNow;
        // Preserve credentials, user/member IDs and prior transactions; only add the staff assignment.
        user.RoleId = role.Id; user.UpdatedAt = now;
        var staff = new StaffProfile { UserId = user.Id, CenterId = centerId, StaffCode = MakeCode("ST"),
            FullName = member.FullName, Position = role.Name, HireDate = request.HireDate, Status = "Active", CreatedAt = now };
        unitOfWork.Context.StaffProfiles.Add(staff);
        CoachProfile? coach = null;
        if (role.Name == RoleNames.Coach)
        {
            coach = new CoachProfile { UserId = user.Id, CenterId = centerId, CoachCode = MakeCode("CO"),
                FullName = member.FullName, Specialization = Normalize(request.Specialization),
                Certification = Normalize(request.Certification), ExperienceYears = request.ExperienceYears,
                Bio = Normalize(request.Bio), HireDate = request.HireDate, Status = "Active", CreatedAt = now };
            unitOfWork.Context.CoachProfiles.Add(coach);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (coach is not null) SetCoachSports(coach, sports);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "staff.appointed", "User", user.Id,
            new { Role = previousRole.Name }, new { Role = role.Name, staff.StaffCode });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(staff, user, role.Name, coach, sports.Select(item => item.Id).ToArray());
    }

    public async Task<StaffResponses.CenterStaffResponse> UpdateAtCenterAsync(
        long actorUserId,
        long centerId,
        long staffUserId,
        StaffRequests.UpdateCenterStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException("Họ tên không được để trống.");
        }
        if (request.HireDate > VietnamTime.Today)
        {
            throw new ValidationException("Ngày nhận việc không thể nằm trong tương lai.");
        }

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await EnsureCanManageStaffAsync(actorUserId, centerId, cancellationToken, allowReadOnlyManager: false);
        var actor = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var actorRole = await unitOfWork.Repository<Role>().GetByIdAsync(actor.RoleId, cancellationToken);
        if (actorUserId == staffUserId && actorRole?.Name == RoleNames.Manager)
        {
            var message = request.Status == "Disabled"
                ? "Bạn không thể khóa tài khoản Manager đang đăng nhập. Hãy nhờ một Manager khác thực hiện."
                : "Bạn không thể sửa thông tin Manager đang đăng nhập tại màn hình Quản lý nhân viên. Hãy dùng Hồ sơ cá nhân hoặc nhờ một Manager khác thực hiện.";
            throw new InvalidOperationException(message)
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = request.Status == "Disabled" ? "STAFF-SELF-LOCK" : "STAFF-SELF-EDIT"
                }
            };
        }
        var staff = await unitOfWork.Context.StaffProfiles.SingleOrDefaultAsync(
            item => item.UserId == staffUserId && item.CenterId == centerId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân sự tại trung tâm này.");
        var user = await unitOfWork.Repository<User>().GetByIdAsync(staffUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken)
            ?? throw new InvalidOperationException("Role của tài khoản không hợp lệ.");
        if (actorRole?.Name == RoleNames.Manager && role.Name == RoleNames.Manager)
        {
            throw new UnauthorizedAccessException("Manager không được sửa tài khoản Manager khác.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? user.Email : request.Email.Trim().ToLowerInvariant();
        var phone = request.Phone is null
            ? user.Phone
            : PhoneNumberNormalization.Normalize(request.Phone);
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new ValidationException("Email không hợp lệ.");
        }
        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            throw new ValidationException("Số điện thoại cần có từ 8 đến 15 chữ số.");
        }
        if (await unitOfWork.Context.Users.AnyAsync(other => other.Id != user.Id
                && (other.Email == email || (phone != null && other.Phone == phone)), cancellationToken))
        {
            throw new InvalidOperationException("Cập nhật thất bại: Email hoặc số điện thoại đã được sử dụng bởi một tài khoản khác trong hệ thống.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "CM-01",
                    ["ProposedHandling"] = "Kiểm tra trùng trước khi lưu; thông báo rõ trường bị trùng và cho người có quyền mở hồ sơ hiện có."
                }
            };
        }
        if (role.Name == RoleNames.Manager && request.Status == "Disabled")
        {
            var activeManagers = await (
                from userRow in unitOfWork.Context.Users
                join managerRole in unitOfWork.Context.Roles on userRow.RoleId equals managerRole.Id
                join profile in unitOfWork.Context.StaffProfiles on userRow.Id equals profile.UserId
                where managerRole.Name == RoleNames.Manager
                      && profile.CenterId == centerId
                      && profile.Status == "Active"
                      && userRow.Status == "Active"
                select userRow.Id).CountAsync(cancellationToken);
            if (activeManagers <= 1)
            {
                throw new InvalidOperationException("Thu hồi quyền thất bại: Hệ thống yêu cầu phải có ít nhất một tài khoản Quản lý (Manager) còn hoạt động tại trung tâm.")
                {
                    Data =
                    {
                        ["StatusCode"] = 409,
                        ["Code"] = "CM-07",
                        ["ProposedHandling"] = "Chặn thao tác; yêu cầu có một tài khoản quản trị thay thế còn hoạt động."
                    }
                };
            }
        }
        if (role.Name == RoleNames.Coach && request.Status != "Active")
        {
            var upcomingSessions = await (
                from a in unitOfWork.Context.ClassCoaches
                join c in unitOfWork.Context.CoachProfiles on a.CoachId equals c.Id
                join cls in unitOfWork.Context.Classes on a.ClassId equals cls.Id
                join s in unitOfWork.Context.ClassSessions on cls.Id equals s.ClassId
                where c.UserId == user.Id
                      && cls.Status != "Cancelled" && cls.Status != "Archived"
                      && s.SessionStatus == "Scheduled"
                      && s.SessionDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
                select new { cls.Name, s.SessionDate }
            ).Take(3).ToListAsync(cancellationToken);

            if (upcomingSessions.Count > 0)
            {
                var summary = string.Join(", ", upcomingSessions.Select(x => $"{x.Name} ({x.SessionDate:dd/MM})"));
                throw new InvalidOperationException($"Không thể ngừng hoạt động HLV đang phụ trách các buổi học sắp tới ({summary}). Yêu cầu phân công người thay thế hoặc xử lý lịch trước khi hoàn tất.")
                {
                    Data =
                    {
                        ["StatusCode"] = 409,
                        ["Code"] = "CM-02",
                        ["ProposedHandling"] = "Hiển thị các buổi bị ảnh hưởng; yêu cầu phân công người thay hoặc xử lý lịch trước khi hoàn tất. Giữ lịch sử giảng dạy."
                    }
                };
            }
        }

        var oldRole = role.Name;
        if (request.RoleName is not null && request.RoleName != role.Name)
        {
            if (role.Name is not (RoleNames.Coach or RoleNames.Receptionist) ||
                request.RoleName is not (RoleNames.Coach or RoleNames.Receptionist))
                throw new UnauthorizedAccessException("Chỉ được đổi giữa vai trò Coach và Receptionist.");
            if (role.Name == RoleNames.Coach && await unitOfWork.Context.ClassCoaches.AnyAsync(a =>
                    unitOfWork.Context.CoachProfiles.Any(c => c.Id == a.CoachId && c.UserId == user.Id) &&
                    unitOfWork.Context.Classes.Any(c => c.Id == a.ClassId && c.Status != "Cancelled" && c.Status != "Archived"), cancellationToken))
                throw new InvalidOperationException("Hãy gỡ phân công các lớp học hoặc phân công người thay thế trước khi đổi vai trò HLV.")
                {
                    Data =
                    {
                        ["StatusCode"] = 409,
                        ["Code"] = "CM-02",
                        ["ProposedHandling"] = "Hiển thị các buổi bị ảnh hưởng; yêu cầu phân công người thay hoặc xử lý lịch trước khi hoàn tất. Giữ lịch sử giảng dạy."
                    }
                };
            role = await unitOfWork.Context.Roles.SingleAsync(r => r.Name == request.RoleName, cancellationToken);
            user.RoleId = role.Id;
        }
        var sports = await ResolveCoachSportsAsync(centerId, role.Name, request.SportIds, cancellationToken);
        var oldStatus = user.Status;
        var now = DateTime.UtcNow;
        staff.FullName = request.FullName.Trim();
        staff.HireDate = request.HireDate;
        staff.Position = role.Name;
        staff.Status = request.Status == "Active" ? "Active" : "Inactive";
        staff.UpdatedAt = now;
        user.Email = email;
        user.Phone = phone;
        user.Status = request.Status;
        user.UpdatedAt = now;
        if (request.Status == "Active")
        {
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
        }
        unitOfWork.Context.StaffProfiles.Update(staff);
        unitOfWork.Context.Users.Update(user);

        var coach = await unitOfWork.Context.CoachProfiles.SingleOrDefaultAsync(
            item => item.UserId == user.Id, cancellationToken);
        if (coach is null && role.Name == RoleNames.Coach)
        {
            coach = new CoachProfile { UserId = user.Id, CenterId = centerId, CoachCode = MakeCode("CO"),
                FullName = staff.FullName, Status = staff.Status, CreatedAt = now };
            unitOfWork.Context.CoachProfiles.Add(coach);
        }
        if (coach is not null)
        {
            coach.FullName = staff.FullName;
            coach.Specialization = Normalize(request.Specialization);
            coach.Certification = Normalize(request.Certification);
            coach.ExperienceYears = request.ExperienceYears;
            coach.Bio = Normalize(request.Bio);
            coach.HireDate = request.HireDate;
            coach.Status = role.Name == RoleNames.Coach ? staff.Status : "Inactive";
            coach.UpdatedAt = now;
            if (coach.Id != 0) unitOfWork.Context.CoachProfiles.Update(coach);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (coach is not null)
        {
            var existingSports = await unitOfWork.Context.CoachSports
                .Where(item => item.CoachId == coach.Id).ToListAsync(cancellationToken);
            var desiredIds = sports.Select(item => item.Id).ToHashSet();
            unitOfWork.Context.CoachSports.RemoveRange(existingSports.Where(item => !desiredIds.Contains(item.SportId)));
            foreach (var sport in sports.Where(item => existingSports.All(current => current.SportId != item.Id)))
                unitOfWork.Context.CoachSports.Add(new CoachSport { CoachId = coach.Id, SportId = sport.Id });
            coach.Specialization = string.Join(", ", sports.Select(item => item.Name));
        }

        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "staff.updated", "User", user.Id,
            new { Status = oldStatus, Role = oldRole }, new { Status = user.Status, Role = role.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(staff, user, role.Name, coach, sports.Select(item => item.Id).ToArray());
    }

    private async Task EnsureCanManageStaffAsync(
        long actorUserId,
        long centerId,
        CancellationToken cancellationToken,
        bool allowReadOnlyManager = false)
    {
        var centerActive = await unitOfWork.Context.Centers.AnyAsync(
            center => center.Id == centerId && center.Status == "Active", cancellationToken);
        var actor = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(actor.RoleId, cancellationToken);
        var assignmentExists = role?.Name == RoleNames.Admin || await unitOfWork.Context.StaffProfiles.AnyAsync(
            profile => profile.UserId == actorUserId && profile.CenterId == centerId && profile.Status == "Active",
            cancellationToken);
        var roleAllowed = role?.Name == RoleNames.Admin
                          || role?.Name == RoleNames.Manager
                          || (allowReadOnlyManager && role?.Name == RoleNames.Receptionist);
        if (actor.Status != "Active" || !centerActive || !assignmentExists || !roleAllowed)
        {
            throw new UnauthorizedAccessException("Không có quyền thao tác nhân sự tại trung tâm này.");
        }
    }

    private static void ValidateCredentials(string username, string email, string password, string? phone)
    {
        username = username.Trim().ToLowerInvariant();
        email = email.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(username, @"^[a-z0-9._-]{3,100}$"))
        {
            throw new ValidationException("Username chỉ được chứa chữ thường, số, dấu chấm, gạch dưới hoặc gạch ngang.");
        }
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new ValidationException("Email không hợp lệ.");
        }
        phone = PhoneNumberNormalization.Normalize(phone);
        PasswordPolicy.ValidateOrThrow(password, email, phone);
        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            throw new ValidationException("Số điện thoại cần có từ 8 đến 15 chữ số.");
        }
    }

    private async Task<List<Sport>> ResolveCoachSportsAsync(long centerId, string roleName,
        IReadOnlyList<long>? requestedIds, CancellationToken cancellationToken)
    {
        var ids = requestedIds?.Where(id => id > 0).Distinct().ToArray() ?? [];
        if (roleName != RoleNames.Coach)
        {
            if (ids.Length > 0) throw new ValidationException("Chỉ Huấn luyện viên mới được chọn chuyên môn.");
            return [];
        }
        if (ids.Length == 0) throw new ValidationException("Huấn luyện viên phải có ít nhất một chuyên môn.");

        var sports = await unitOfWork.Context.Sports
            .Where(item => ids.Contains(item.Id) && (item.CenterId == null || item.CenterId == centerId)
                && item.Status == "Active")
            .OrderBy(item => item.Name).ToListAsync(cancellationToken);
        if (sports.Count != ids.Length)
            throw new ValidationException("Có chuyên môn không tồn tại, đã ngừng hoạt động hoặc không thuộc trung tâm này.");
        return sports;
    }

    private void SetCoachSports(CoachProfile coach, IReadOnlyCollection<Sport> sports)
    {
        coach.Specialization = string.Join(", ", sports.Select(item => item.Name));
        foreach (var sport in sports)
            unitOfWork.Context.CoachSports.Add(new CoachSport { CoachId = coach.Id, SportId = sport.Id });
    }

    private static StaffResponses.CenterStaffResponse Map(StaffProfile staff, User user, string roleName, CoachProfile? coach,
        IReadOnlyList<long>? sportIds = null, int? activeClassCount = null) =>
        new(user.Id, staff.CenterId, staff.StaffCode, coach?.CoachCode, user.Username, user.Email, user.Phone,
            roleName, staff.FullName, user.Status, staff.HireDate, coach?.Specialization,
            coach?.Certification, coach?.ExperienceYears, coach?.Bio, sportIds ?? [], activeClassCount);

    private static string MakeCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

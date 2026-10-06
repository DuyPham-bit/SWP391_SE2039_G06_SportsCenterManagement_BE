using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StaffRequests = SportsCenterManagement.BLL.DTOs.Staff.Requests;
using StaffResponses = SportsCenterManagement.BLL.DTOs.Staff.Responses;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
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
        CancellationToken cancellationToken = default)
    {
        await EnsureCanManageStaffAsync(actorUserId, centerId, cancellationToken, allowReadOnlyManager: true);
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100
            || query?.Trim().Length > 100)
        {
            throw new ValidationException("Page cần từ 1; pageSize từ 1 đến 100; từ khóa tối đa 100 ký tự.");
        }

        var term = query?.Trim();
        var rows = await (
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
            orderby staff.FullName
            select new { Staff = staff, User = user, RoleName = role.Name, Coach = coach })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return rows.Select(row => Map(row.Staff, row.User, row.RoleName, row.Coach)).ToArray();
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
        if (request.HireDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException("Ngày nhận việc không thể nằm trong tương lai.");
        }

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await EnsureCanManageStaffAsync(actorUserId, centerId, cancellationToken);
        var actor = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var actorRole = await unitOfWork.Repository<Role>().GetByIdAsync(actor.RoleId, cancellationToken);
        if (request.RoleName == RoleNames.SystemAdmin ||
            (request.RoleName == RoleNames.Manager && actorRole?.Name != RoleNames.SystemAdmin) ||
            (actorRole?.Name == RoleNames.Manager && request.RoleName is not (RoleNames.Coach or RoleNames.Receptionist)))
        {
            throw new UnauthorizedAccessException("Manager chỉ được tạo Coach hoặc Receptionist tại trung tâm mình quản lý.");
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = Normalize(request.Phone);
        if (await unitOfWork.Context.Users.AnyAsync(user => user.Username == normalizedUsername
                                                             || user.Email == normalizedEmail
                                                             || (normalizedPhone != null && user.Phone == normalizedPhone), cancellationToken))
        {
            throw new InvalidOperationException("Username, email hoặc số điện thoại đã được sử dụng.");
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
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "staff.created", "User", user.Id,
            newValues: new { Role = request.RoleName, staff.StaffCode, staff.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(staff, user, role.Name, coach);
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
        if (request.HireDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException("Ngày nhận việc không thể nằm trong tương lai.");
        }

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await EnsureCanManageStaffAsync(actorUserId, centerId, cancellationToken, allowReadOnlyManager: false);
        var staff = await unitOfWork.Context.StaffProfiles.SingleOrDefaultAsync(
            item => item.UserId == staffUserId && item.CenterId == centerId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân sự tại trung tâm này.");
        var user = await unitOfWork.Repository<User>().GetByIdAsync(staffUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken)
            ?? throw new InvalidOperationException("Role của tài khoản không hợp lệ.");
        var actor = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var actorRole = await unitOfWork.Repository<Role>().GetByIdAsync(actor.RoleId, cancellationToken);
        if (actorRole?.Name == RoleNames.Manager && role.Name == RoleNames.Manager)
        {
            throw new UnauthorizedAccessException("Manager không được sửa tài khoản Manager khác.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? user.Email : request.Email.Trim().ToLowerInvariant();
        var phone = Normalize(request.Phone);
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
            throw new InvalidOperationException("Email hoặc số điện thoại đã được sử dụng.");
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
                throw new InvalidOperationException("Không thể vô hiệu hóa Manager cuối cùng của trung tâm.");
            }
        }

        var oldStatus = user.Status;
        var now = DateTime.UtcNow;
        staff.FullName = request.FullName.Trim();
        staff.HireDate = request.HireDate;
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
        if (coach is not null)
        {
            coach.FullName = staff.FullName;
            coach.Specialization = Normalize(request.Specialization);
            coach.Certification = Normalize(request.Certification);
            coach.ExperienceYears = request.ExperienceYears;
            coach.Bio = Normalize(request.Bio);
            coach.HireDate = request.HireDate;
            coach.Status = staff.Status;
            coach.UpdatedAt = now;
            unitOfWork.Context.CoachProfiles.Update(coach);
        }

        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "staff.updated", "User", user.Id,
            new { Status = oldStatus }, new { Status = user.Status, Role = role.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(staff, user, role.Name, coach);
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
        var assignmentExists = role?.Name == RoleNames.SystemAdmin || await unitOfWork.Context.StaffProfiles.AnyAsync(
            profile => profile.UserId == actorUserId && profile.CenterId == centerId && profile.Status == "Active",
            cancellationToken);
        var roleAllowed = role?.Name == RoleNames.SystemAdmin
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
        if (password.Length < 12 || !Regex.IsMatch(password, "[A-Z]") || !Regex.IsMatch(password, "[a-z]")
            || !Regex.IsMatch(password, "[0-9]") || !Regex.IsMatch(password, "[^a-zA-Z0-9]"))
        {
            throw new ValidationException("Mật khẩu cần ít nhất 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.");
        }
        if (phone is not null && !Regex.IsMatch(phone.Trim(), @"^\+?[0-9]{8,15}$"))
        {
            throw new ValidationException("Số điện thoại cần có từ 8 đến 15 chữ số.");
        }
    }

    private static StaffResponses.CenterStaffResponse Map(StaffProfile staff, User user, string roleName, CoachProfile? coach) =>
        new(user.Id, staff.CenterId, staff.StaffCode, coach?.CoachCode, user.Username, user.Email, user.Phone,
            roleName, staff.FullName, user.Status, staff.HireDate, coach?.Specialization,
            coach?.Certification, coach?.ExperienceYears, coach?.Bio);

    private static string MakeCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

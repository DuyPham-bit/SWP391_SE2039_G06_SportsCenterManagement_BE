using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AuthRequests = SportsCenterManagement.BLL.DTOs.Auth.Requests;
using AuthResponses = SportsCenterManagement.BLL.DTOs.Auth.Responses;
using MembersRequests = SportsCenterManagement.BLL.DTOs.Members.Requests;
using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class MemberService(IUnitOfWork unitOfWork, IAuthService authService) : IMemberService
{
    public async Task<MembersResponses.MemberProfileResponse> GetMeAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await ReadProfile(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
    }

    public async Task<MembersResponses.MemberProfileResponse> UpdateMeAsync(
        long userId,
        MembersRequests.UpdateMemberProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await unitOfWork.Repository<MemberProfile>()
            .Find(item => item.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        return await UpdateProfileAsync(userId, profile.CenterId, profile.Id, request, "member.profile.updated", cancellationToken);
    }

    public Task<MembersResponses.MemberProfileResponse> UpdateAtCenterAsync(
        long actorUserId,
        long centerId,
        long memberId,
        MembersRequests.UpdateMemberProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        return UpdateProfileAsync(actorUserId, centerId, memberId, request, "member.profile.updated.by_staff", cancellationToken);
    }

    private async Task<MembersResponses.MemberProfileResponse> UpdateProfileAsync(
        long actorUserId,
        long? requiredCenterId,
        long memberId,
        MembersRequests.UpdateMemberProfileRequest request,
        string action,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException("Họ tên không được để trống.");
        }
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException("Ngày sinh không được ở tương lai.");
        }

        if (request.Gender is not null && request.Gender is not ("Male" or "Female" or "Other"))
        {
            throw new ValidationException("Giới tính không hợp lệ.");
        }

        var phone = PhoneNumberNormalization.Normalize(request.Phone);
        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            throw new ValidationException("Số điện thoại cần có từ 8 đến 15 chữ số.");
        }
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (email is not null && !new EmailAddressAttribute().IsValid(email))
        {
            throw new ValidationException("Email không hợp lệ.");
        }

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var profile = await unitOfWork.Repository<MemberProfile>()
            .GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        if (requiredCenterId.HasValue)
        {
            if (profile.CenterId != requiredCenterId)
            {
                throw new KeyNotFoundException("Không tìm thấy thành viên tại trung tâm này.");
            }
            if (profile.UserId != actorUserId)
            {
                await EnsureCenterManagerAsync(actorUserId, requiredCenterId.Value, cancellationToken);
            }
        }
        var user = await unitOfWork.Repository<User>().GetByIdAsync(profile.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        if (phone is not null && await unitOfWork.Repository<User>()
                .AnyAsync(item => item.Id != user.Id && item.Phone == phone, cancellationToken))
        {
            throw new InvalidOperationException("Số điện thoại đã được sử dụng.");
        }
        if (email is not null && await unitOfWork.Repository<User>()
                .AnyAsync(item => item.Id != user.Id && item.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Email đã được sử dụng.");
        }

        var oldEmail = user.Email;
        var oldPhone = user.Phone;
        var oldNormalizedPhone = PhoneNumberNormalization.Normalize(oldPhone);
        var phoneChanged = request.Phone is not null && oldNormalizedPhone != phone;
        profile.FullName = request.FullName.Trim();
        if (request.DateOfBirth.HasValue)
            profile.DateOfBirth = request.DateOfBirth;
        if (request.Gender is not null)
            profile.Gender = Normalize(request.Gender);
        if (request.Address is not null)
            profile.Address = Normalize(request.Address);
        profile.UpdatedAt = DateTime.UtcNow;
        if (email is not null)
        {
            user.Email = email;
        }
        if (request.Phone is not null)
        {
            user.Phone = phone;
        }
        user.UpdatedAt = DateTime.UtcNow;
        unitOfWork.Repository<MemberProfile>().Update(profile);
        unitOfWork.Repository<User>().Update(user);
        AuditLogWriter.Add(unitOfWork, actorUserId, profile.CenterId, action, "MemberProfile", profile.Id,
            new { EmailPresent = oldEmail is not null, PhonePresent = oldPhone is not null },
            new { EmailChanged = email is not null && email != oldEmail, PhoneChanged = phoneChanged });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(profile, user);
    }

    public async Task<MembersResponses.MemberProfileResponse> SetMemberStatusAsync(
        long actorUserId,
        long centerId,
        long memberId,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (status is not ("Active" or "Disabled"))
        {
            throw new ValidationException("Trạng thái tài khoản chỉ nhận Active hoặc Disabled.");
        }

        await EnsureCenterManagerAsync(actorUserId, centerId, cancellationToken);
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var profile = await unitOfWork.Repository<MemberProfile>().GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy thành viên tại trung tâm này.");
        if (profile.CenterId != centerId)
        {
            throw new KeyNotFoundException("Không tìm thấy thành viên tại trung tâm này.");
        }
        var user = await unitOfWork.Repository<User>().GetByIdAsync(profile.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        var previousStatus = user.Status;
        if (previousStatus == status)
        {
            await transaction.CommitAsync(cancellationToken);
            return Map(profile, user);
        }

        user.Status = status;
        user.UpdatedAt = DateTime.UtcNow;
        if (status == "Active")
        {
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
        }
        unitOfWork.Repository<User>().Update(user);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "member.account_status.updated", "User", user.Id,
            new { Status = previousStatus }, new { Status = status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(profile, user);
    }

    public async Task<MembersResponses.PagedResponse<MembersResponses.MemberProfileResponse>> SearchAtCenterAsync(
        long actorUserId,
        long centerId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (centerId <= 0 || string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2 || query.Length > 100
            || page is < 1 or > 1_000_000 || pageSize is < 1 or > 50)
        {
            throw new ValidationException("Từ khóa cần từ 2 đến 100 ký tự; page từ 1 và pageSize từ 1 đến 50.");
        }
        await EnsureCenterStaffAsync(actorUserId, centerId, cancellationToken);

        var term = query.Trim();
        // Lọc center trước khi phân trang để không lộ member của center khác.
        var matches =
            from profile in unitOfWork.Context.MemberProfiles.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on profile.UserId equals user.Id
            where profile.CenterId == centerId
                  && (profile.MemberCode.Contains(term)
                      || profile.FullName.Contains(term)
                      || user.Email.Contains(term)
                      || (user.Phone != null && user.Phone.Contains(term)))
            orderby profile.FullName
            select new MembersResponses.MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
                user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
                profile.Address, profile.CreatedAt, user.Status);
        var totalCount = await matches.CountAsync(cancellationToken);
        var items = await matches.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new MembersResponses.PagedResponse<MembersResponses.MemberProfileResponse>(items, page, pageSize, totalCount);
    }

    public async Task<MembersResponses.MemberProfileResponse> CreateAtCenterAsync(
        long actorUserId,
        long centerId,
        AuthRequests.RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCenterStaffAsync(actorUserId, centerId, cancellationToken);
        return await authService.RegisterAsync(request, centerId, actorUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<MembersResponses.MemberSubscriptionResponse>> GetSubscriptionsAsync(
        long actorUserId,
        long memberId,
        CancellationToken cancellationToken = default)
    {
        var member = await unitOfWork.Repository<MemberProfile>().GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        if (member.UserId != actorUserId)
        {
            if (!member.CenterId.HasValue)
            {
                throw new UnauthorizedAccessException("Không có quyền xem hồ sơ thành viên này.");
            }
            await EnsureCenterStaffAsync(actorUserId, member.CenterId.Value, cancellationToken);
        }

        return await (
            from subscription in unitOfWork.Context.MemberSubscriptions.AsNoTracking()
            join package in unitOfWork.Context.MembershipPackages.AsNoTracking() on subscription.PackageId equals package.Id
            where subscription.MemberId == memberId
            orderby subscription.CreatedAt descending
            select new MembersResponses.MemberSubscriptionResponse(subscription.Id, package.Id, package.Name,
                subscription.Price, subscription.DurationDays, subscription.StartDate,
                subscription.EndDate, subscription.Status, subscription.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureCanSellToMemberAsync(
        long actorUserId,
        long memberId,
        long packageId,
        CancellationToken cancellationToken = default)
    {
        var target = await unitOfWork.Repository<MemberProfile>().GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        var package = await unitOfWork.Repository<MembershipPackage>().GetByIdAsync(packageId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy gói tập.");

        if (!target.CenterId.HasValue || target.CenterId.Value != package.CenterId)
        {
            throw new UnauthorizedAccessException("Thành viên và gói tập không thuộc cùng trung tâm.");
        }
        await EnsureCenterStaffAsync(actorUserId, package.CenterId, cancellationToken);
    }

    public async Task EnsureCanBuyPackageAsync(long userId, long packageId, CancellationToken cancellationToken = default)
    {
        var member = await unitOfWork.Repository<MemberProfile>()
            .Find(profile => profile.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        var user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken);
        if (user?.Status != "Active" || user.LockedUntil > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Tài khoản không hoạt động.");
        }
        var package = await unitOfWork.Repository<MembershipPackage>()
            .Find(item => item.Id == packageId && item.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy gói tập.");
        if (!await unitOfWork.Repository<Center>()
                .AnyAsync(center => center.Id == package.CenterId && center.Status == "Active", cancellationToken))
        {
            throw new KeyNotFoundException("Không tìm thấy trung tâm đang hoạt động.");
        }

        if (member.CenterId.HasValue && member.CenterId.Value != package.CenterId)
        {
            throw new UnauthorizedAccessException("Gói tập không thuộc trung tâm của thành viên.");
        }
        if (package.Price <= 0 || package.DurationDays <= 0)
        {
            throw new ValidationException("Gói tập có giá hoặc thời hạn không hợp lệ.");
        }
    }

    public async Task EnsureCanProcessInvoiceAsync(
        long actorUserId,
        long invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await unitOfWork.Repository<Invoice>().GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
        await EnsureCenterStaffAsync(actorUserId, invoice.CenterId, cancellationToken);
    }

    private async Task EnsureCenterStaffAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var centerIsActive = await unitOfWork.Repository<Center>()
            .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken);
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken);
        if (user.Status != "Active" || user.LockedUntil > DateTime.UtcNow || !centerIsActive
            || role?.Name is not ("Manager" or "Receptionist"))
        {
            throw new UnauthorizedAccessException("Chỉ Manager hoặc Receptionist được thao tác tại quầy.");
        }

        var assigned = await unitOfWork.Repository<StaffProfile>()
            .AnyAsync(profile => profile.UserId == actorUserId
                                 && profile.CenterId == centerId
                                 && profile.Status == "Active", cancellationToken);
        if (!assigned)
        {
            throw new UnauthorizedAccessException("Tài khoản không được gán vào trung tâm này.");
        }
    }

    private async Task EnsureCenterManagerAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var centerIsActive = await unitOfWork.Repository<Center>()
            .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken);
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken);
        var assigned = await unitOfWork.Repository<StaffProfile>()
            .AnyAsync(profile => profile.UserId == actorUserId
                                 && profile.CenterId == centerId
                                 && profile.Status == "Active", cancellationToken);
        if (user.Status != "Active" || user.LockedUntil > DateTime.UtcNow
            || !centerIsActive || role?.Name != "Manager" || !assigned)
        {
            throw new UnauthorizedAccessException("Chỉ Manager được quản lý thành viên của trung tâm được gán.");
        }
    }

    private async Task<MembersResponses.MemberProfileResponse?> ReadProfile(long userId, CancellationToken cancellationToken)
    {
        return await (
            from profile in unitOfWork.Context.MemberProfiles.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on profile.UserId equals user.Id
            where user.Id == userId && user.Status == "Active"
            select new MembersResponses.MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
                user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
                profile.Address, profile.CreatedAt, user.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static MembersResponses.MemberProfileResponse Map(MemberProfile profile, User user)
    {
        return new MembersResponses.MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
            user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
            profile.Address, profile.CreatedAt, user.Status);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class MemberService(IUnitOfWork unitOfWork, IAuthService authService) : IMemberService
{
    public async Task<MemberProfileResponse> GetMeAsync(long userId, CancellationToken cancellationToken = default)
    {
        // 1. Lấy thông tin hồ sơ và tài khoản active từ DB
        return await ReadProfile(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
    }

    public async Task<MemberProfileResponse> UpdateMeAsync(
        long userId,
        UpdateMemberProfileRequest request,
        CancellationToken cancellationToken = default)
    {   
        // 1. Kiểm tra ràng buộc dữ liệu: ngày sinh không vượt quá hiện tại
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException("Ngày sinh không được ở tương lai.");
        }

        // 2. Kiểm tra giá trị giới tính hợp lệ
        if (request.Gender is not null && request.Gender is not ("Male" or "Female" or "Other"))
        {
            throw new ValidationException("Giới tính không hợp lệ.");
        }

        // 3. Chuẩn hóa và kiểm tra định dạng số điện thoại
        var phone = Normalize(request.Phone);
        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            throw new ValidationException("Số điện thoại cần có từ 8 đến 15 chữ số.");
        }

        // 4. Mở transaction Serializable chống xung đột khi cập nhật đồng thời
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        // 5. Tìm hồ sơ hội viên và tài khoản người dùng tương ứng
        var profile = await unitOfWork.Repository<MemberProfile>()
            .Find(item => item.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        var user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        // 6. Đảm bảo số điện thoại mới chưa bị tài khoản khác sử dụng
        if (phone is not null && await unitOfWork.Repository<User>()
                .AnyAsync(item => item.Id != userId && item.Phone == phone, cancellationToken))
        {
            throw new InvalidOperationException("Số điện thoại đã được sử dụng.");
        }

        // 7. Cập nhật thông tin cá nhân và lưu thay đổi vào DB
        profile.FullName = request.FullName.Trim();
        profile.DateOfBirth = request.DateOfBirth;
        profile.Gender = Normalize(request.Gender);
        profile.Address = Normalize(request.Address);
        profile.UpdatedAt = DateTime.UtcNow;
        user.Phone = phone;
        user.UpdatedAt = DateTime.UtcNow;
        unitOfWork.Repository<MemberProfile>().Update(profile);
        unitOfWork.Repository<User>().Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. Hoàn tất transaction và trả về hồ sơ sau cập nhật
        await transaction.CommitAsync(cancellationToken);
        return Map(profile, user);
    }

    public async Task<PagedResponse<MemberProfileResponse>> SearchAtCenterAsync(
        long actorUserId,
        long centerId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ của tham số tìm kiếm và phân trang
        if (centerId <= 0 || string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2 || query.Length > 100
            || page is < 1 or > 1_000_000 || pageSize is < 1 or > 50)
        {
            throw new ValidationException("Từ khóa cần từ 2 đến 100 ký tự; page từ 1 và pageSize từ 1 đến 50.");
        }

        // 2. Xác thực quyền nhân viên (Manager/Receptionist) tại trung tâm
        await EnsureCenterStaffAsync(actorUserId, centerId, cancellationToken);

        var term = query.Trim();

        // 3. Lọc danh sách hội viên thuộc đúng trung tâm theo từ khóa (Mã, Tên, Email, SĐT)
        var matches =
            from profile in unitOfWork.Context.MemberProfiles.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on profile.UserId equals user.Id
            where profile.CenterId == centerId
                  && (profile.MemberCode.Contains(term)
                      || profile.FullName.Contains(term)
                      || user.Email.Contains(term)
                      || (user.Phone != null && user.Phone.Contains(term)))
            orderby profile.FullName
            select new MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
                user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
                profile.Address, profile.CreatedAt);

        // 4. Đếm tổng số bản ghi và lấy dữ liệu trang hiện tại
        var totalCount = await matches.CountAsync(cancellationToken);
        var items = await matches.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResponse<MemberProfileResponse>(items, page, pageSize, totalCount);
    }

    public async Task<MemberProfileResponse> CreateAtCenterAsync(
        long actorUserId,
        long centerId,
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra quyền nhân viên thao tác tại trung tâm
        await EnsureCenterStaffAsync(actorUserId, centerId, cancellationToken);

        // 2. Chuyển tiếp sang AuthService để tạo User và hồ sơ Member tại trung tâm
        return await authService.RegisterAsync(request, centerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MemberSubscriptionResponse>> GetSubscriptionsAsync(
        long actorUserId,
        long memberId,
        CancellationToken cancellationToken = default)
    {
        // 1. Lấy thông tin hồ sơ hội viên
        var member = await unitOfWork.Repository<MemberProfile>().GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");

        // 2. Kiểm tra quyền xem: chính chủ hội viên hoặc nhân viên thuộc trung tâm quản lý
        if (member.UserId != actorUserId)
        {
            if (!member.CenterId.HasValue)
            {
                throw new UnauthorizedAccessException("Không có quyền xem hồ sơ thành viên này.");
            }
            await EnsureCenterStaffAsync(actorUserId, member.CenterId.Value, cancellationToken);
        }

        // 3. Lấy danh sách các gói tập đã đăng ký theo thứ tự mới nhất
        return await (
            from subscription in unitOfWork.Context.MemberSubscriptions.AsNoTracking()
            join package in unitOfWork.Context.MembershipPackages.AsNoTracking() on subscription.PackageId equals package.Id
            where subscription.MemberId == memberId
            orderby subscription.CreatedAt descending
            select new MemberSubscriptionResponse(subscription.Id, package.Id, package.Name,
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
        // 1. Kiểm tra tồn tại của hội viên và gói tập
        var target = await unitOfWork.Repository<MemberProfile>().GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        var package = await unitOfWork.Repository<MembershipPackage>().GetByIdAsync(packageId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy gói tập.");

        // 2. Đảm bảo hội viên và gói tập thuộc cùng một trung tâm
        if (!target.CenterId.HasValue || target.CenterId.Value != package.CenterId)
        {
            throw new UnauthorizedAccessException("Thành viên và gói tập không thuộc cùng trung tâm.");
        }

        // 3. Kiểm tra nhân viên thao tác có thuộc trung tâm đó hay không
        await EnsureCenterStaffAsync(actorUserId, package.CenterId, cancellationToken);
    }

    public async Task EnsureCanBuyPackageAsync(long userId, long packageId, CancellationToken cancellationToken = default)
    {
        // 1. Tìm hồ sơ hội viên và kiểm tra tài khoản đang ở trạng thái Active
        var member = await unitOfWork.Repository<MemberProfile>()
            .Find(profile => profile.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ thành viên.");
        var user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken);
        if (user?.Status != "Active")
        {
            throw new UnauthorizedAccessException("Tài khoản không hoạt động.");
        }

        // 2. Tìm gói tập đang Active và đảm bảo trung tâm phát hành gói đang hoạt động
        var package = await unitOfWork.Repository<MembershipPackage>()
            .Find(item => item.Id == packageId && item.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy gói tập.");
        if (!await unitOfWork.Repository<Center>()
                .AnyAsync(center => center.Id == package.CenterId && center.Status == "Active", cancellationToken))
        {
            throw new KeyNotFoundException("Không tìm thấy trung tâm đang hoạt động.");
        }

        // 3. Đảm bảo gói tập thuộc đúng trung tâm của hội viên
        if (member.CenterId.HasValue && member.CenterId.Value != package.CenterId)
        {
            throw new UnauthorizedAccessException("Gói tập không thuộc trung tâm của thành viên.");
        }

        // 4. Kiểm tra tính hợp lệ về giá và thời hạn của gói
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
        // 1. Kiểm tra tồn tại của hóa đơn
        var invoice = await unitOfWork.Repository<Invoice>().GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");

        // 2. Kiểm tra nhân viên thu ngân có quyền tại trung tâm của hóa đơn
        await EnsureCenterStaffAsync(actorUserId, invoice.CenterId, cancellationToken);
    }

    private async Task EnsureCenterStaffAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        // 1. Lấy thông tin tài khoản người dùng và trạng thái trung tâm
        var user = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var centerIsActive = await unitOfWork.Repository<Center>()
            .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken);
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken);

        // 2. Kiểm tra vai trò hợp lệ: chỉ Manager hoặc Receptionist
        if (user.Status != "Active" || !centerIsActive || role?.Name is not ("Manager" or "Receptionist"))
        {
            throw new UnauthorizedAccessException("Chỉ Manager hoặc Receptionist được thao tác tại quầy.");
        }

        // 3. Đảm bảo nhân viên đã được gán vào trung tâm tương ứng
        var assigned = await unitOfWork.Repository<StaffProfile>()
            .AnyAsync(profile => profile.UserId == actorUserId
                                 && profile.CenterId == centerId
                                 && profile.Status == "Active", cancellationToken);
        if (!assigned)
        {
            throw new UnauthorizedAccessException("Tài khoản không được gán vào trung tâm này.");
        }
    }

    private async Task<MemberProfileResponse?> ReadProfile(long userId, CancellationToken cancellationToken)
    {
        return await (
            from profile in unitOfWork.Context.MemberProfiles.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on profile.UserId equals user.Id
            where user.Id == userId && user.Status == "Active"
            select new MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
                user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
                profile.Address, profile.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static MemberProfileResponse Map(MemberProfile profile, User user)
    {
        return new MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
            user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
            profile.Address, profile.CreatedAt);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

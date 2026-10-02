using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class AuthService(IUnitOfWork unitOfWork) : IAuthService
{
    public async Task<MemberProfileResponse> RegisterAsync(
        RegisterRequest request,
        long? centerId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ và độ phức tạp của mật khẩu trước khi xử lý
        ValidatePassword(request.Password);
        centerId ??= request.CenterId;

        // Chuẩn hóa tên đăng nhập và email về chữ thường để kiểm tra trùng lặp chính xác
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        // Kiểm tra định dạng tên đăng nhập (chỉ gồm chữ cái, chữ số, dấu chấm, gạch dưới, gạch ngang)
        if (username.Length is < 3 or > 100 || !Regex.IsMatch(username, @"^[a-z0-9._-]+$"))
        {
            throw new ValidationException("Username can only contain letters, numbers, dots, underscores, or hyphens.");
        }

        // Kiểm tra định dạng email hợp lệ
        if (!new EmailAddressAttribute().IsValid(email))
        {
            throw new ValidationException("Invalid email format.");
        }

        // Chuẩn hóa và kiểm tra tính hợp lệ của số điện thoại
        var phone = NormalizeOptional(request.Phone);
        ValidatePhone(phone);

        // Kiểm tra ngày sinh không được vượt quá ngày hiện tại
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException("Date of birth cannot be in the future.");
        }

        var users = unitOfWork.Repository<User>();

        // Đảm bảo tên đăng nhập hoặc email chưa được đăng ký trong hệ thống
        if (await users.AnyAsync(user => user.Username == username || user.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Username or email is already in use.");
        }

        // Đảm bảo số điện thoại chưa được đăng ký nếu người dùng có cung cấp
        if (phone is not null && await users.AnyAsync(user => user.Phone == phone, cancellationToken))
        {
            throw new InvalidOperationException("Phone number is already in use.");
        }

        // Lấy vai trò mặc định 'Member' từ cơ sở dữ liệu
        var role = await unitOfWork.Repository<Role>()
            .Find(item => item.Name == "Member")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Member role configuration is missing.");

        // Nếu có chỉ định trung tâm, kiểm tra trung tâm tồn tại và đang hoạt động
        if (centerId.HasValue && !await unitOfWork.Repository<Center>()
                .AnyAsync(center => center.Id == centerId.Value && center.Status == "Active", cancellationToken))
        {
            throw new InvalidOperationException("Center does not exist or is inactive.");
        }

        // Bắt đầu transaction: Tạo User và MemberProfile phải cùng thành công hoặc cùng rollback
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Tạo thực thể người dùng mới kèm mật khẩu đã được băm an toàn
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = PasswordHashing.Hash(request.Password),
            Phone = phone,
            RoleId = role.Id,
            Status = "Active",
            CreatedAt = now
        };
        await users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Tạo hồ sơ hội viên liên kết trực tiếp với người dùng vừa tạo
        var profile = new MemberProfile
        {
            UserId = user.Id,
            CenterId = centerId,
            MemberCode = $"MB{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            CreatedAt = now
        };
        await unitOfWork.Repository<MemberProfile>().AddAsync(profile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Xác nhận lưu dữ liệu (Commit transaction) khi mọi thao tác thành công
        await transaction.CommitAsync(cancellationToken);

        return new MemberProfileResponse(profile.Id, user.Id, profile.MemberCode, user.Username,
            user.Email, user.Phone, profile.FullName, profile.DateOfBirth, profile.Gender,
            profile.Address, profile.CreatedAt);
    }

    public async Task<AuthenticatedUser> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Chuẩn hóa thông tin định danh đăng nhập (username hoặc email) về chữ thường
        var login = request.Login.Trim().ToLowerInvariant();

        // Tìm kiếm tài khoản người dùng theo tên đăng nhập hoặc địa chỉ email
        var user = await unitOfWork.Repository<User>()
            .Find(item => item.Username == login || item.Email == login)
            .SingleOrDefaultAsync(cancellationToken);

        // Nếu không tìm thấy user, thực hiện băm giả định (dummy hash) để phòng chống tấn công Timing Attack
        if (user is null)
        {
            _ = PasswordHashing.Verify(request.Password, DummyPasswordHash.Value);
            throw new InvalidOperationException("Invalid username or password.");
        }

        var now = DateTime.UtcNow;

        // Kiểm tra xem tài khoản có đang bị vô hiệu hóa hoặc bị tạm khóa hay không
        if (user.Status != "Active" || user.LockedUntil > now)
        {
            throw new InvalidOperationException("Account is currently unavailable for login.");
        }

        // Kiểm tra mật khẩu người dùng nhập vào khớp với hash đã lưu trong DB
        if (!PasswordHashing.Verify(request.Password, user.PasswordHash))
        {
            // Tăng số lần đăng nhập sai; nếu sai liên tiếp 5 lần thì khóa tài khoản trong 15 phút
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.FailedLoginAttempts = 0;
                user.LockedUntil = now.AddMinutes(15);
            }
            unitOfWork.Repository<User>().Update(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invalid username or password.");
        }

        // Đặt lại số lần đăng nhập sai về 0 và cập nhật thời điểm đăng nhập gần nhất khi thành công
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        unitOfWork.Repository<User>().Update(user);

        // Lấy thông tin tên vai trò (Role) của người dùng
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken)
            ?? throw new InvalidOperationException("User role is no longer valid.");

        // Lấy mã trung tâm trực thuộc nếu người dùng là nhân viên đang hoạt động
        var centerId = await unitOfWork.Repository<StaffProfile>()
            .Find(profile => profile.UserId == user.Id && profile.Status == "Active")
            .Select(profile => (long?)profile.CenterId)
            .SingleOrDefaultAsync(cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthenticatedUser(user.Id, user.Username, role.Name, centerId);
    }

    /// <summary>
    /// Kiểm tra độ phức tạp của mật khẩu: tối thiểu 12 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
    /// </summary>
    /// <param name="password">Chuỗi mật khẩu gốc cần kiểm tra.</param>
    private static void ValidatePassword(string password)
    {
        // 1. Kiểm tra chuỗi rỗng hoặc độ dài tối thiểu 12 ký tự
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12
            // 2. Phải chứa ít nhất 1 chữ hoa (A-Z)
            || !Regex.IsMatch(password, "[A-Z]")
            // 3. Phải chứa ít nhất 1 chữ thường (a-z)
            || !Regex.IsMatch(password, "[a-z]")
            // 4. Phải chứa ít nhất 1 chữ số (0-9)
            || !Regex.IsMatch(password, "[0-9]")
            // 5. Phải chứa ít nhất 1 ký tự đặc biệt
            || !Regex.IsMatch(password, "[^a-zA-Z0-9]"))
        {
            // Ném ngoại lệ nếu không thỏa mãn bất kỳ điều kiện bảo mật nào
            throw new ValidationException("Password must be at least 12 characters long and contain uppercase letters, lowercase letters, numbers, and special characters.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidatePhone(string? phone)
    {
        if (phone is not null && !Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
        {
            throw new ValidationException("Phone number must contain between 8 and 15 digits.");
        }
    }

    private static class DummyPasswordHash
    {
        public static readonly string Value = PasswordHashing.Hash("Dummy-Password-Not-For-Login-123!");
    }
}

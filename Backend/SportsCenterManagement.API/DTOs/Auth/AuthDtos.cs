using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.API.DTOs.Auth;

/// <summary>
/// Thông tin tài khoản gửi lên để xác thực đăng nhập.
/// </summary>
public sealed record LoginRequest
{
    /// <summary>
    /// Địa chỉ email đăng ký tài khoản.
    /// </summary>
    [Required, EmailAddress]
    public required string Email { get; init; }

    /// <summary>
    /// Mật khẩu tài khoản.
    /// </summary>
    [Required]
    public required string Password { get; init; }
}

/// <summary>
/// Yêu cầu thay đổi mật khẩu của người dùng đã đăng nhập.
/// </summary>
public sealed record ChangePasswordRequest
{
    /// <summary>
    /// Mật khẩu hiện tại để xác thực chủ tài khoản.
    /// </summary>
    [Required]
    public required string CurrentPassword { get; init; }

    /// <summary>
    /// Mật khẩu mới mong muốn (8 - 128 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt).
    /// </summary>
    [Required, MinLength(8), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>
/// Phản hồi kết quả xác thực danh tính sau khi đăng nhập hoặc đổi mật khẩu thành công.
/// </summary>
/// <param name="AccessToken">Chuỗi Bearer Token xác thực người dùng.</param>
/// <param name="UserId">Mã định danh người dùng trong hệ thống.</param>
/// <param name="Role">Vai trò của người dùng (ADMIN, MANAGER, MEMBER, COACH, STAFF).</param>
/// <param name="ExpiresAt">Thời điểm token hết hạn.</param>
public sealed record AuthResponse(string AccessToken, long UserId, string Role, DateTimeOffset ExpiresAt);

/// <summary>
/// Thông tin chi tiết hồ sơ cá nhân của người dùng đang đăng nhập.
/// </summary>
/// <param name="UserId">Mã định danh tài khoản người dùng.</param>
/// <param name="Email">Địa chỉ email liên lạc.</param>
/// <param name="FullName">Họ và tên người dùng.</param>
/// <param name="Phone">Số điện thoại.</param>
/// <param name="Role">Vai trò của người dùng.</param>
/// <param name="Status">Trạng thái tài khoản (Active, Suspended,...).</param>
/// <param name="MemberCode">Mã hội viên (nếu người dùng là Hội viên).</param>
/// <param name="CreatedAt">Ngày tạo tài khoản.</param>
public sealed record ProfileResponse(
    long UserId,
    string Email,
    string FullName,
    string? Phone,
    string Role,
    string Status,
    string? MemberCode,
    DateTimeOffset CreatedAt);

/// <summary>
/// Dữ liệu yêu cầu cập nhật hồ sơ cá nhân của người dùng.
/// </summary>
public sealed record UpdateProfileRequest
{
    /// <summary>
    /// Họ và tên mới.
    /// </summary>
    [Required, MaxLength(150)]
    public required string FullName { get; init; }

    /// <summary>
    /// Số điện thoại liên lạc mới (10 số, bắt đầu bằng 0).
    /// </summary>
    [MaxLength(20)]
    public string? Phone { get; init; }
}

/// <summary>
/// Yêu cầu gửi mã OTP để đặt lại mật khẩu quên.
/// </summary>
public sealed record RequestPasswordResetRequest
{
    /// <summary>
    /// Địa chỉ email tài khoản cần khôi phục mật khẩu.
    /// </summary>
    [Required, EmailAddress]
    public required string Email { get; init; }
}

/// <summary>
/// Dữ liệu xác nhận OTP và đặt lại mật khẩu mới.
/// </summary>
public sealed record ResetPasswordRequest
{
    /// <summary>
    /// Địa chỉ email tài khoản cần đặt lại mật khẩu.
    /// </summary>
    [Required, EmailAddress]
    public required string Email { get; init; }

    /// <summary>
    /// Mã OTP 6 chữ số đã gửi qua email (hoặc môi trường dev).
    /// </summary>
    [Required, MinLength(4), MaxLength(10)]
    public required string Otp { get; init; }

    /// <summary>
    /// Mật khẩu mới cần thiết lập.
    /// </summary>
    [Required, MinLength(8), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>
/// Kết quả phản hồi sau khi yêu cầu mã khôi phục mật khẩu.
/// </summary>
/// <param name="Message">Thông điệp hướng dẫn người dùng.</param>
/// <param name="DevelopmentOtp">Mã OTP trả về trực tiếp trong môi trường phát triển (Development) để tiện kiểm thử.</param>
public sealed record PasswordResetRequestResponse(string Message, string? DevelopmentOtp);

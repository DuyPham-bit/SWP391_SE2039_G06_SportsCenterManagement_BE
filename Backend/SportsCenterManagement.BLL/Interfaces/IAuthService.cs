using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ xác thực người dùng và đăng ký tài khoản.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Đăng ký tài khoản hội viên mới và tạo hồ sơ cá nhân ban đầu.
    /// </summary>
    /// <param name="request">Thông tin đăng ký hội viên.</param>
    /// <param name="centerId">Mã trung tâm thể thao trực thuộc (nếu có).</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Hồ sơ cá nhân của hội viên vừa tạo.</returns>
    Task<MemberProfileResponse> RegisterAsync(
        RegisterRequest request,
        long? centerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực thông tin đăng nhập và kiểm tra trạng thái khóa tài khoản.
    /// </summary>
    /// <param name="request">Email hoặc tên đăng nhập và mật khẩu.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin người dùng sau khi xác thực thành công.</returns>
    Task<AuthenticatedUser> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

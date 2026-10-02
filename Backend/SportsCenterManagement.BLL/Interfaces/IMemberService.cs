using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ quản lý hồ sơ và các thao tác liên quan đến hội viên (Member).
/// </summary>
public interface IMemberService
{
    /// <summary>
    /// Lấy thông tin hồ sơ của hội viên theo UserId.
    /// </summary>
    Task<MemberProfileResponse> GetMeAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật thông tin hồ sơ cá nhân của hội viên.
    /// </summary>
    Task<MemberProfileResponse> UpdateMeAsync(long userId, UpdateMemberProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm danh sách hội viên tại trung tâm theo từ khóa có phân trang.
    /// </summary>
    Task<PagedResponse<MemberProfileResponse>> SearchAtCenterAsync(
        long actorUserId,
        long centerId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo mới hồ sơ hội viên tại trung tâm do nhân viên thực hiện.
    /// </summary>
    Task<MemberProfileResponse> CreateAtCenterAsync(long actorUserId, long centerId, RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách lịch sử gói tập của một hội viên.
    /// </summary>
    Task<IReadOnlyList<MemberSubscriptionResponse>> GetSubscriptionsAsync(long actorUserId, long memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra điều kiện hợp lệ để hội viên có thể mua gói tập (Gói đang Active, không trùng gói đang hiệu lực).
    /// </summary>
    Task EnsureCanBuyPackageAsync(long userId, long packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra quyền hạn của nhân viên đối với hóa đơn cần xử lý thanh toán.
    /// </summary>
    Task EnsureCanProcessInvoiceAsync(long actorUserId, long invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra điều kiện để nhân viên có thể bán gói tập cho hội viên tại quầy.
    /// </summary>
    Task EnsureCanSellToMemberAsync(long actorUserId, long memberId, long packageId, CancellationToken cancellationToken = default);
}

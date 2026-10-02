using SportsCenterManagement.BLL.DTOs.MembershipPackages;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ quản lý gói tập thành viên (Membership Packages).
/// </summary>
public interface IMembershipPackageService
{
    /// <summary>
    /// Lấy danh sách các gói tập đang mở bán (Active) tại trung tâm thể thao.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Danh sách gói tập.</returns>
    Task<IReadOnlyList<MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo mới một gói tập thể thao tại trung tâm.
    /// </summary>
    /// <param name="actorUserId">ID người dùng thực hiện tạo (Quản lý).</param>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="request">Thông tin cấu hình gói tập.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin chi tiết gói tập vừa tạo.</returns>
    Task<MembershipPackageResponse> CreateAsync(
        long actorUserId,
        long centerId,
        CreateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật thông tin và trạng thái một gói tập thể thao.
    /// </summary>
    /// <param name="actorUserId">ID người dùng thực hiện cập nhật.</param>
    /// <param name="packageId">Mã định danh gói tập cần sửa.</param>
    /// <param name="request">Dữ liệu thông tin cập nhật mới.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin gói tập sau khi cập nhật.</returns>
    Task<MembershipPackageResponse> UpdateAsync(
        long actorUserId,
        long packageId,
        UpdateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);
}

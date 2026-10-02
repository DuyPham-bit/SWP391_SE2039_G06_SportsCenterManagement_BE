using SportsCenterManagement.BLL.DTOs.CoreFlows;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các luồng nghiệp vụ cốt lõi xuyên suốt hệ thống (Core Flows: Đăng ký gói, Ghi nhận tiền mặt, Điểm danh/Đăng ký lớp, Doanh thu).
/// </summary>
public interface ICoreFlowService
{
    /// <summary>
    /// Lấy danh sách các gói tập thể thao đang hoạt động của trung tâm.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Danh sách gói tập.</returns>
    Task<IReadOnlyList<MembershipPackage>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo đơn đăng ký gói tập ở trạng thái chờ thanh toán kèm hóa đơn tạm thời.
    /// </summary>
    /// <param name="memberId">Mã định danh hội viên.</param>
    /// <param name="packageId">Mã định danh gói tập.</param>
    /// <param name="createdBy">ID người tạo (null nếu hội viên tự đăng ký online).</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Kết quả đơn chờ thanh toán và thông tin hóa đơn.</returns>
    Task<PendingMembershipResult> CreatePendingMembershipAsync(
        long memberId,
        long packageId,
        long? createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng ký ghi danh một hội viên vào lớp học dựa trên gói tập còn hiệu lực.
    /// </summary>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="memberId">Mã định danh hội viên.</param>
    /// <param name="subscriptionId">Mã đăng ký gói tập còn hạn.</param>
    /// <param name="registeredBy">ID người ghi nhận đăng ký.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Bản ghi ghi danh lớp học.</returns>
    Task<ClassEnrollment> EnrollMemberAsync(
        long classId,
        long memberId,
        long subscriptionId,
        long? registeredBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận thanh toán tiền mặt tại quầy cho một hóa đơn với khóa chống trùng lặp.
    /// </summary>
    /// <param name="invoiceId">Mã định danh hóa đơn.</param>
    /// <param name="processedBy">ID nhân viên thu ngân / lễ tân thực hiện.</param>
    /// <param name="amount">Số tiền thu.</param>
    /// <param name="idempotencyKey">Khóa chống thanh toán trùng.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Bản ghi thanh toán đã được lưu trong DB.</returns>
    Task<Payment> RecordCashPaymentAsync(
        long invoiceId,
        long processedBy,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tính toán báo cáo doanh thu của trung tâm thể thao trong một khoảng thời gian.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="from">Ngày bắt đầu kỳ báo cáo.</param>
    /// <param name="to">Ngày kết thúc kỳ báo cáo.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thống kê tổng doanh thu, tiền hoàn và doanh thu thuần.</returns>
    Task<RevenueSummary> GetRevenueAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

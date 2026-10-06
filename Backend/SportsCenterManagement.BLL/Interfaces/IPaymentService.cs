using SportsCenterManagement.BLL.DTOs.Payments;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ xử lý thanh toán qua cổng VNPay.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Tạo hóa đơn tạm và sinh ra đường dẫn (URL) chuyển hướng sang cổng VNPay.
    /// </summary>
    /// <param name="memberId">ID của Member đang đăng nhập</param>
    /// <param name="request">Thông tin gói tập và phương thức do Frontend gửi lên</param>
    /// <param name="ipAddress">Địa chỉ IP của Client</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ</param>
    /// <returns>Chuỗi URL thanh toán của VNPay</returns>
    Task<string> CreatePaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xử lý kết quả phản hồi từ VNPay (Callback / IPN), xác thực signature và cập nhật trạng thái đơn hàng trong DB.
    /// </summary>
    /// <param name="queryParams">Tập hợp các tham số key-value do VNPay gửi về qua URL</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ</param>
    /// <returns>Kết quả thanh toán chi tiết để thông báo cho người dùng</returns>
    Task<PaymentResultResponse> ProcessPaymentCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);
}

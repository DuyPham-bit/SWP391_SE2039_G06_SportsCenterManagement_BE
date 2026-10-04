using SportsCenterManagement.BLL.DTOs.Payments;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface chuyên trách việc tích hợp kỹ thuật với cổng thanh toán VNPay:
/// - Đóng gói tham số theo chuẩn VNPay.
/// - Sắp xếp Alphabetical A-Z.
/// - Băm mã bảo mật HMAC-SHA512 để sinh Payment URL.
/// - Giải mã và kiểm tra chữ ký số từ phản hồi của VNPay.
/// </summary>
public interface IVnPayService
{
    /// <summary>
    /// Tạo đường link chuyển hướng sang cổng thanh toán VNPay.
    /// </summary>
    /// <param name="invoiceNumber">Mã hóa đơn duy nhất của hệ thống (vnp_TxnRef)</param>
    /// <param name="amount">Số tiền thanh toán (VNĐ)</param>
    /// <param name="orderDescription">Nội dung mô tả thanh toán</param>
    /// <param name="ipAddress">Địa chỉ IP của người dùng</param>
    /// <param name="bankCode">Mã ngân hàng (tùy chọn, ví dụ: NCB, VNPAYQR)</param>
    /// <returns>URL thanh toán VNPay hoàn chỉnh</returns>
    string CreatePaymentUrl(
        string invoiceNumber,
        decimal amount,
        string orderDescription,
        string ipAddress,
        string? bankCode = null);

    /// <summary>
    /// Phân tích dữ liệu phản hồi từ VNPay (Callback/IPN) và kiểm tra tính toàn vẹn chữ ký.
    /// </summary>
    /// <param name="queryParams">Tập hợp các tham số URL do VNPay gửi về</param>
    /// <returns>Đối tượng kết quả giải mã từ VNPay</returns>
    VnPayCallbackResult ProcessCallback(IDictionary<string, string> queryParams);
}

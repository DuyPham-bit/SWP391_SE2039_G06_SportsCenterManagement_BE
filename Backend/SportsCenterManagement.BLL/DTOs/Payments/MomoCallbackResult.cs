namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO chứa kết quả sau khi giải mã và xác thực chữ ký bảo mật từ MoMo Callback/IPN.
/// </summary>
public class MomoCallbackResult
{
    /// <summary>
    /// Chữ ký số từ MoMo có hợp lệ hay không (chống giả mạo dữ liệu).
    /// </summary>
    public bool IsValidSignature { get; set; }

    /// <summary>
    /// Giao dịch thành công hay không (ResultCode == 0).
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Mã trạng thái từ MoMo (0: Thành công, 9000: Đã xác nhận, 1006: User hủy...).
    /// </summary>
    public int ResultCode { get; set; }

    /// <summary>
    /// Thông điệp phản hồi từ MoMo.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Mã hóa đơn nội bộ hệ thống gửi sang (InvoiceNumber).
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Mã yêu cầu duy nhất cho giao dịch.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Mã giao dịch thực tế trên hệ thống MoMo (transId).
    /// </summary>
    public string TransId { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền thực tế thanh toán (VNĐ).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Phương thức thanh toán (ví dụ: qr, momo_wallet, credit).
    /// </summary>
    public string PayType { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung mô tả đơn hàng.
    /// </summary>
    public string OrderInfo { get; set; } = string.Empty;
}

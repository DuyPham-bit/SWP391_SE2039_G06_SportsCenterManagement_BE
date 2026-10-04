namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO chứa kết quả sau khi giải mã và xác thực chữ ký số từ cổng VNPay.
/// </summary>
public class VnPayCallbackResult
{
    /// <summary>
    /// Chữ ký số từ VNPay có hợp lệ hay không (chống giả mạo dữ liệu).
    /// </summary>
    public bool IsValidSignature { get; set; }

    /// <summary>
    /// Giao dịch thành công hay không (Mã phản hồi vnp_ResponseCode == "00").
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Mã lỗi hoặc mã trạng thái từ VNPay (ví dụ: "00", "24", "97").
    /// </summary>
    public string ResponseCode { get; set; } = string.Empty;

    /// <summary>
    /// Mã hóa đơn nội bộ của hệ thống gửi sang VNPay (vnp_TxnRef).
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Mã giao dịch do cổng VNPay sinh ra (vnp_TransactionNo).
    /// </summary>
    public string TransactionNo { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền thực tế của giao dịch (VNĐ).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Mã ngân hàng mà người dùng đã chọn để thanh toán (vnp_BankCode).
    /// </summary>
    public string BankCode { get; set; } = string.Empty;
}

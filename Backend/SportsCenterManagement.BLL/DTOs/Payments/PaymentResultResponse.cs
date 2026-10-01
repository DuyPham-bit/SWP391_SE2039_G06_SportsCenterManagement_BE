namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO chứa kết quả phản hồi sau khi Backend xử lý kết quả thanh toán từ VNPay.
/// </summary>
public class PaymentResultResponse
{
    /// <summary>
    /// Trạng thái thanh toán: true nếu thành công, false nếu thất bại/hủy.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Thông điệp mô tả chi tiết trạng thái giao dịch.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Mã hóa đơn nội bộ của hệ thống (ví dụ: "SC-20261001-XXXX").
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// Mã giao dịch do cổng VNPay sinh ra (vnp_TransactionNo) để đối soát.
    /// </summary>
    public string? TransactionId { get; set; }

    /// <summary>
    /// Số tiền thực tế của giao dịch (VNĐ).
    /// </summary>
    public decimal Amount { get; set; }
}

namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO trả về cho Frontend khi khởi tạo giao dịch thanh toán VietQR / PayOS.
/// </summary>
public class VietQrPaymentResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = "Khởi tạo mã VietQR thành công.";
    public string InvoiceNumber { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
}

namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// Cấu hình thông tin tích hợp Cổng thanh toán VietQR / PayOS từ appsettings.json.
/// </summary>
public class PayOsOption
{
    public const string SectionName = "PayOS";

    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

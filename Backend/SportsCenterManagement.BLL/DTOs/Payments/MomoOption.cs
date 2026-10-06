namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// Cấu hình thông tin tích hợp Cổng thanh toán MoMo từ appsettings.json.
/// </summary>
public class MomoOption
{
    public const string SectionName = "Momo";

    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string NotifyUrl { get; set; } = string.Empty;
    public string RequestType { get; set; } = "captureWallet";
}

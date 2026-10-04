using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Service cài đặt kỹ thuật tích hợp với cổng thanh toán VNPay.
/// Tuân thủ chuẩn bảo mật HMAC-SHA512 và chuẩn tham số của VNPay v2.1.0.
/// </summary>
public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;

    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Đóng gói các tham số, băm chữ ký SHA512 và sinh đường dẫn chuyển hướng sang VNPay.
    /// </summary>
    public string CreatePaymentUrl(
        string invoiceNumber,
        decimal amount,
        string orderDescription,
        string ipAddress,
        string? bankCode = null)
    {
        var vnpay = new VnPayLibrary();
        var tmnCode = _configuration["VnPay:TmnCode"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:TmnCode");
        var hashSecret = _configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:HashSecret");
        var baseUrl = _configuration["VnPay:BaseUrl"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:BaseUrl");
        var returnUrl = _configuration["VnPay:ReturnUrl"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:ReturnUrl");

        // VNPay quy định đơn vị tiền tệ nhân 100
        var amountInVnpayFormat = ((long)(amount * 100)).ToString();

        // Chuẩn hóa địa chỉ IP
        var clientIp = (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1" || ipAddress.Contains(':'))
            ? "127.0.0.1"
            : ipAddress;

        var now = DateTime.UtcNow;

        vnpay.AddRequestData("vnp_Version", _configuration["VnPay:Version"] ?? "2.1.0");
        vnpay.AddRequestData("vnp_Command", _configuration["VnPay:Command"] ?? "pay");
        vnpay.AddRequestData("vnp_TmnCode", tmnCode);
        vnpay.AddRequestData("vnp_Amount", amountInVnpayFormat);
        vnpay.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", _configuration["VnPay:CurrCode"] ?? "VND");
        vnpay.AddRequestData("vnp_IpAddr", clientIp);
        vnpay.AddRequestData("vnp_Locale", _configuration["VnPay:Locale"] ?? "vn");
        vnpay.AddRequestData("vnp_OrderInfo", orderDescription);
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnpay.AddRequestData("vnp_TxnRef", invoiceNumber);

        if (!string.IsNullOrEmpty(bankCode))
        {
            vnpay.AddRequestData("vnp_BankCode", bankCode);
        }

        return vnpay.CreateRequestUrl(baseUrl, hashSecret);
    }

    /// <summary>
    /// Giải mã dữ liệu trả về từ VNPay và kiểm tra chữ ký số.
    /// </summary>
    public VnPayCallbackResult ProcessCallback(IDictionary<string, string> queryParams)
    {
        var vnpay = new VnPayLibrary();
        foreach (var (key, value) in queryParams)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
            {
                vnpay.AddResponseData(key, value);
            }
        }

        var hashSecret = _configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:HashSecret");
        if (!queryParams.TryGetValue("vnp_SecureHash", out var vnpSecureHash) || string.IsNullOrEmpty(vnpSecureHash))
        {
            return new VnPayCallbackResult
            {
                IsValidSignature = false,
                IsSuccess = false,
                ResponseCode = "NO_HASH"
            };
        }

        // Kiểm tra chữ ký HMAC-SHA512
        var isValidSignature = vnpay.ValidateSignature(vnpSecureHash, hashSecret);
        var responseCode = vnpay.GetResponseData("vnp_ResponseCode");
        var invoiceNumber = vnpay.GetResponseData("vnp_TxnRef");
        var transactionNo = vnpay.GetResponseData("vnp_TransactionNo");
        var bankCode = vnpay.GetResponseData("vnp_BankCode");
        var rawAmount = vnpay.GetResponseData("vnp_Amount");
        var amount = string.IsNullOrEmpty(rawAmount) ? 0 : Convert.ToDecimal(rawAmount) / 100;

        return new VnPayCallbackResult
        {
            IsValidSignature = isValidSignature,
            IsSuccess = isValidSignature && responseCode == "00",
            ResponseCode = responseCode,
            InvoiceNumber = invoiceNumber,
            TransactionNo = transactionNo,
            BankCode = bankCode,
            Amount = amount
        };
    }
}

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Service cài đặt kỹ thuật kết nối trực tiếp với Cổng thanh toán MoMo API v2.
/// </summary>
public class MoMoService : IMoMoService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public MoMoService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Tạo yêu cầu thanh toán sang MoMo và nhận về URL giao dịch.
    /// </summary>
    public async Task<MomoCreatePaymentResponse> CreatePaymentUrlAsync(
        string orderId,
        decimal amount,
        string orderInfo,
        CancellationToken cancellationToken = default)
    {
        var partnerCode = _configuration["Momo:PartnerCode"] ?? throw new InvalidOperationException("Chưa cấu hình Momo:PartnerCode");
        var accessKey = _configuration["Momo:AccessKey"] ?? throw new InvalidOperationException("Chưa cấu hình Momo:AccessKey");
        var secretKey = _configuration["Momo:SecretKey"] ?? throw new InvalidOperationException("Chưa cấu hình Momo:SecretKey");
        var paymentUrl = _configuration["Momo:PaymentUrl"] ?? "https://test-payment.momo.vn/v2/gateway/api/create";
        var redirectUrl = _configuration["Momo:ReturnUrl"] ?? "http://localhost:5173/payment-result";
        var ipnUrl = _configuration["Momo:NotifyUrl"] ?? "http://localhost:54162/api/payments/momo-ipn";
        var requestType = _configuration["Momo:RequestType"] ?? "captureWallet";

        var requestId = Guid.NewGuid().ToString();
        var extraData = string.Empty;
        var amountLong = (long)amount;

        // Sinh chữ ký bảo mật từ thư viện MomoSecurity
        var signature = MomoSecurity.CreateRequestSignature(
            accessKey: accessKey,
            amount: amountLong,
            extraData: extraData,
            ipnUrl: ipnUrl,
            orderId: orderId,
            orderInfo: orderInfo,
            partnerCode: partnerCode,
            redirectUrl: redirectUrl,
            requestId: requestId,
            requestType: requestType,
            secretKey: secretKey);

        var requestPayload = new
        {
            partnerCode,
            partnerName = "Sports Center Management",
            storeId = "SportsCenterStore",
            requestId,
            amount = amountLong,
            orderId,
            orderInfo,
            redirectUrl,
            ipnUrl,
            lang = "vi",
            extraData,
            requestType,
            signature
        };

        var serializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        };

        var jsonPayload = JsonSerializer.Serialize(requestPayload, serializerOptions);
        using var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(paymentUrl, requestContent, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Lỗi gọi sang cổng MoMo: {response.StatusCode} - {responseContent}");
        }

        var momoResponse = JsonSerializer.Deserialize<MomoCreatePaymentResponse>(
            responseContent,
            serializerOptions);

        return momoResponse ?? throw new InvalidOperationException("Không thể đọc phản hồi từ MoMo.");
    }

    /// <summary>
    /// Phân tích dữ liệu phản hồi từ MoMo và xác thực chữ ký HMAC-SHA256.
    /// </summary>
    public MomoCallbackResult ProcessCallback(IDictionary<string, string> queryParams)
    {
        var accessKey = _configuration["Momo:AccessKey"] ?? string.Empty;
        var secretKey = _configuration["Momo:SecretKey"] ?? string.Empty;

        // Xác thực chữ ký phản hồi từ MoMo qua MomoSecurity
        var isValidSignature = MomoSecurity.VerifyCallbackSignature(queryParams, accessKey, secretKey);

        queryParams.TryGetValue("orderId", out var orderId);
        queryParams.TryGetValue("requestId", out var requestId);
        queryParams.TryGetValue("amount", out var rawAmount);
        queryParams.TryGetValue("orderInfo", out var orderInfo);
        queryParams.TryGetValue("transId", out var transId);
        queryParams.TryGetValue("resultCode", out var rawResultCode);
        queryParams.TryGetValue("message", out var message);
        queryParams.TryGetValue("payType", out var payType);

        int.TryParse(rawResultCode, out var resultCode);
        decimal.TryParse(rawAmount, out var amount);

        return new MomoCallbackResult
        {
            IsValidSignature = isValidSignature,
            IsSuccess = isValidSignature && resultCode == 0,
            ResultCode = resultCode,
            Message = message ?? string.Empty,
            OrderId = orderId ?? string.Empty,
            RequestId = requestId ?? string.Empty,
            TransId = transId ?? string.Empty,
            Amount = amount,
            PayType = payType ?? string.Empty,
            OrderInfo = orderInfo ?? string.Empty
        };
    }
}

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Net.Http.Json;
using System.Globalization;
using SportsCenterManagement.BLL.Common;
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
        var redirectUrl = _configuration["Momo:ReturnUrl"] ?? "http://localhost:54162/payment-result";
        var ipnUrl = _configuration["Momo:NotifyUrl"] ?? "http://localhost:54162/api/payments/momo-ipn";
        var requestType = _configuration["Momo:RequestType"] ?? "captureWallet";

        var requestId = orderId;
        var extraData = string.Empty;
        if (amount <= 0 || amount != decimal.Truncate(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "MoMo requires a positive whole-VND amount.");
        }
        var amountLong = decimal.ToInt64(amount);

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
        queryParams.TryGetValue("partnerCode", out var partnerCode);
        var expectedPartnerCode = _configuration["Momo:PartnerCode"];
        isValidSignature = isValidSignature &&
                           !string.IsNullOrWhiteSpace(accessKey) &&
                           !string.IsNullOrWhiteSpace(secretKey) &&
                           !string.IsNullOrWhiteSpace(expectedPartnerCode) &&
                           string.Equals(partnerCode, expectedPartnerCode, StringComparison.Ordinal);

        queryParams.TryGetValue("orderId", out var orderId);
        queryParams.TryGetValue("requestId", out var requestId);
        queryParams.TryGetValue("amount", out var rawAmount);
        queryParams.TryGetValue("orderInfo", out var orderInfo);
        queryParams.TryGetValue("transId", out var transId);
        queryParams.TryGetValue("resultCode", out var rawResultCode);
        queryParams.TryGetValue("message", out var message);
        queryParams.TryGetValue("payType", out var payType);

        int.TryParse(rawResultCode, out var resultCode);
        decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);

        return new MomoCallbackResult
        {
            IsValidSignature = isValidSignature,
            IsSuccess = isValidSignature && resultCode == 0,
            IsPending = isValidSignature && resultCode == 7002,
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

    public async Task<ProviderRefundResult> RefundAsync(
        string requestId,
        string refundOrderId,
        string providerTransactionId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var partnerCode = _configuration["Momo:PartnerCode"] ?? throw new InvalidOperationException("Chưa cấu hình Momo:PartnerCode");
        var accessKey = _configuration["Momo:AccessKey"] ?? throw new InvalidOperationException("Chưa cấu hình Momo:AccessKey");
        var secretKey = _configuration["Momo:SecretKey"] ?? throw new InvalidOperationException("Chưa cấu hình Momo:SecretKey");
        var refundUrl = _configuration["Momo:RefundUrl"] ?? "https://test-payment.momo.vn/v2/gateway/api/refund";
        if (amount < 1_000m || amount > 50_000_000m || amount != decimal.Truncate(amount))
        {
            throw BusinessException.BadRequest("MoMo refund phải là số VND nguyên trong phạm vi 1.000 đến 50.000.000.");
        }
        if (!long.TryParse(providerTransactionId, NumberStyles.None, CultureInfo.InvariantCulture, out var originalTransactionId) ||
            originalTransactionId <= 0)
        {
            throw BusinessException.Conflict("Mã giao dịch gốc MoMo không hợp lệ để gửi yêu cầu refund.");
        }
        var amountVnd = decimal.ToInt64(amount);
        var description = reason.Trim();
        var signature = MomoSecurity.CreateRefundRequestSignature(
            accessKey, amountVnd, description, refundOrderId, partnerCode, requestId,
            providerTransactionId, secretKey);

        var payload = new
        {
            partnerCode,
            orderId = refundOrderId,
            requestId,
            amount = amountVnd,
            transId = originalTransactionId,
            lang = "vi",
            description,
            signature
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var response = await _httpClient.PostAsJsonAsync(refundUrl, payload, timeout.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MoMo refund request failed with HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var resultCode = root.TryGetProperty("resultCode", out var codeElement) && codeElement.TryGetInt32(out var code)
            ? code
            : int.MinValue;
        var returnedOrderId = root.TryGetProperty("orderId", out var orderElement) ? orderElement.GetString() : null;
        var returnedRequestId = root.TryGetProperty("requestId", out var requestElement) ? requestElement.GetString() : null;
        var returnedPartnerCode = root.TryGetProperty("partnerCode", out var partnerElement) ? partnerElement.GetString() : null;
        var returnedAmount = root.TryGetProperty("amount", out var amountElement) && amountElement.TryGetInt64(out var parsedAmount)
            ? parsedAmount
            : -1L;
        var returnedTransactionId = root.TryGetProperty("transId", out var transIdElement) && transIdElement.TryGetInt64(out var parsedTransactionId)
            ? parsedTransactionId
            : 0L;
        var message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty;

        var authenticated = string.Equals(returnedPartnerCode, partnerCode, StringComparison.Ordinal) &&
                            string.Equals(returnedOrderId, refundOrderId, StringComparison.Ordinal) &&
                            string.Equals(returnedRequestId, requestId, StringComparison.Ordinal) &&
                            returnedAmount == amountVnd &&
                            (resultCode != 0 || returnedTransactionId > 0);
        var status = resultCode == 0 ? "Succeeded" : resultCode == 7002 ? "Pending" : "Failed";
        var providerRefundId = resultCode == 0
            ? returnedTransactionId.ToString(CultureInfo.InvariantCulture)
            : null;
        return new ProviderRefundResult(authenticated, status, providerRefundId, message);
    }
}

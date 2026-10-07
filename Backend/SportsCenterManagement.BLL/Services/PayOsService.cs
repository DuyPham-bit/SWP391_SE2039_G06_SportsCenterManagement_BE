using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Service chuyên trách gọi API PayOS để sinh mã VietQR và kiểm tra Webhook.
/// </summary>
public class PayOsService : IPayOsService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public PayOsService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<PayOsCreatePaymentResponse> CreatePaymentLinkAsync(
        long orderCode,
        decimal amount,
        string description,
        string itemName,
        CancellationToken cancellationToken = default)
    {
        var clientId = RequireConfiguration("PayOS:ClientId");
        var apiKey = RequireConfiguration("PayOS:ApiKey");
        var checksumKey = RequireConfiguration("PayOS:ChecksumKey");
        var baseUrl = _configuration["PayOS:BaseUrl"] ?? "https://api-merchant.payos.vn";
        var returnUrl = RequireAbsoluteUrl("PayOS:ReturnUrl");
        var cancelUrl = RequireAbsoluteUrl("PayOS:CancelUrl");

        if (orderCode <= 0 || amount <= 0 || amount != decimal.Truncate(amount) || amount > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "PayOS requires a positive whole-VND amount within Int32 range and a positive order code.");
        }

        if (description.Length is < 1 or > 25 || description.Any(character => character > 127))
        {
            throw new ArgumentException("PayOS description must contain 1 to 25 ASCII characters.", nameof(description));
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("PayOS:BaseUrl must be an absolute HTTPS URL.");
        }

        var amountInt = decimal.ToInt32(amount);

        // 1. Sinh chữ ký bảo mật cho request tạo thanh toán
        var signature = PayOsSecurity.CreateRequestSignature(
            amount: amountInt,
            cancelUrl: cancelUrl,
            description: description,
            orderCode: orderCode,
            returnUrl: returnUrl,
            checksumKey: checksumKey);

        // 2. Chuẩn bị payload gửi sang PayOS API
        var requestPayload = new
        {
            orderCode,
            amount = amountInt,
            description,
            items = new[]
            {
                new { name = itemName, quantity = 1, price = amountInt }
            },
            cancelUrl,
            returnUrl,
            signature
        };

        var serializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        };

        var jsonContent = JsonSerializer.Serialize(requestPayload, serializerOptions);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/v2/payment-requests"))
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        // Gắn header xác thực bắt buộc của PayOS
        requestMessage.Headers.Add("x-client-id", clientId);
        requestMessage.Headers.Add("x-api-key", apiKey);

        // 3. Thực thi gọi API sang PayOS
        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Lỗi kết nối PayOS: {response.StatusCode} - {responseContent}");
        }

        var payOsResponse = JsonSerializer.Deserialize<PayOsCreatePaymentResponse>(responseContent, serializerOptions);

        if (payOsResponse == null || payOsResponse.Code != "00" || payOsResponse.Data == null)
        {
            throw new InvalidOperationException($"Không thể tạo mã VietQR từ PayOS: {payOsResponse?.Desc ?? "Phản hồi không hợp lệ"}");
        }

        var paymentData = payOsResponse.Data;
        if (paymentData.OrderCode != orderCode || paymentData.Amount != amountInt ||
            !string.Equals(paymentData.Currency, "VND", StringComparison.Ordinal) ||
            !Uri.TryCreate(paymentData.CheckoutUrl, UriKind.Absolute, out var checkoutUri) ||
            checkoutUri.Scheme != Uri.UriSchemeHttps ||
            !(checkoutUri.Host.Equals("payos.vn", StringComparison.OrdinalIgnoreCase) ||
              checkoutUri.Host.EndsWith(".payos.vn", StringComparison.OrdinalIgnoreCase)) ||
            !PayOsSecurity.VerifyWebhookSignature(paymentData, payOsResponse.Signature, checksumKey))
        {
            throw new InvalidOperationException("PayOS returned a payment link that failed signature or transaction validation.");
        }

        return payOsResponse;
    }

    public bool VerifyWebhookSignature(PayOsWebhookRequest webhookRequest)
    {
        var checksumKey = _configuration["PayOS:ChecksumKey"] ?? string.Empty;
        if (webhookRequest.Data == null || string.IsNullOrWhiteSpace(webhookRequest.Signature))
        {
            return false;
        }

        return PayOsSecurity.VerifyWebhookSignature(
            data: webhookRequest.Data,
            signature: webhookRequest.Signature,
            checksumKey: checksumKey);
    }

    private string RequireConfiguration(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("your-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Chưa cấu hình {key} bằng biến môi trường hoặc User Secrets.");
        }

        return value;
    }

    private string RequireAbsoluteUrl(string key)
    {
        var value = RequireConfiguration(key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{key} phải là URL tuyệt đối dùng HTTP hoặc HTTPS.");
        }

        return uri.AbsoluteUri;
    }
}

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
        var clientId = _configuration["PayOS:ClientId"] ?? throw new InvalidOperationException("Chưa cấu hình PayOS:ClientId");
        var apiKey = _configuration["PayOS:ApiKey"] ?? throw new InvalidOperationException("Chưa cấu hình PayOS:ApiKey");
        var checksumKey = _configuration["PayOS:ChecksumKey"] ?? throw new InvalidOperationException("Chưa cấu hình PayOS:ChecksumKey");
        var baseUrl = _configuration["PayOS:BaseUrl"] ?? "https://api-merchant.payos.vn";
        var returnUrl = _configuration["PayOS:ReturnUrl"] ?? "http://localhost:5173/payment-result";
        var cancelUrl = _configuration["PayOS:CancelUrl"] ?? "http://localhost:5173/payment-result?cancel=true";

        var amountInt = (int)amount;

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
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v2/payment-requests")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        // Gắn header xác thực bắt buộc của PayOS
        requestMessage.Headers.Add("x-client-id", clientId);
        requestMessage.Headers.Add("x-api-key", apiKey);

        // 3. Thực thi gọi API sang PayOS
        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
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
}

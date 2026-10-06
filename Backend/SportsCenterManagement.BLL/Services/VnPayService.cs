using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
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
    private readonly HttpClient _httpClient;

    public VnPayService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
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
        var transactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
        var currencyCode = vnpay.GetResponseData("vnp_CurrCode");
        var terminalCode = vnpay.GetResponseData("vnp_TmnCode");
        var expectedTerminalCode = _configuration["VnPay:TmnCode"];
        var validProvider = isValidSignature && !string.IsNullOrWhiteSpace(expectedTerminalCode) &&
                            string.Equals(terminalCode, expectedTerminalCode, StringComparison.Ordinal);
        var invoiceNumber = vnpay.GetResponseData("vnp_TxnRef");
        var transactionNo = vnpay.GetResponseData("vnp_TransactionNo");
        var bankCode = vnpay.GetResponseData("vnp_BankCode");
        var rawAmount = vnpay.GetResponseData("vnp_Amount");
        var amount = long.TryParse(rawAmount, NumberStyles.None, CultureInfo.InvariantCulture, out var amountMinor)
            ? amountMinor / 100m
            : 0m;

        return new VnPayCallbackResult
        {
            IsValidSignature = validProvider,
            IsSuccess = validProvider && responseCode == "00" && (transactionStatus is "" or "00"),
            IsPending = validProvider && (responseCode == "09" || transactionStatus == "01"),
            ResponseCode = responseCode,
            TransactionStatus = transactionStatus,
            CurrencyCode = currencyCode,
            InvoiceNumber = invoiceNumber,
            TransactionNo = transactionNo,
            BankCode = bankCode,
            Amount = amount
        };
    }

    public async Task<ProviderRefundResult> RefundAsync(
        string paymentReference,
        string requestId,
        string providerTransactionId,
        bool isFullRefund,
        DateTime paidAtUtc,
        decimal amount,
        string reason,
        string requestedBy,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tmnCode = _configuration["VnPay:TmnCode"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:TmnCode");
        var hashSecret = _configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:HashSecret");
        var apiUrl = _configuration["VnPay:RefundUrl"] ?? "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
        var nowLocal = DateTime.UtcNow.AddHours(7);
        var paymentDateLocal = paidAtUtc.AddHours(7);
        var amountMinor = decimal.ToInt64(amount * 100m);
        var transactionType = isFullRefund ? "02" : "03";
        var cleanIpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress;
        var safeReason = reason.Trim();

        var signatureData = string.Join('|', new[]
        {
            requestId,
            _configuration["VnPay:Version"] ?? "2.1.0",
            "refund",
            tmnCode,
            transactionType,
            paymentReference,
            amountMinor.ToString(CultureInfo.InvariantCulture),
            providerTransactionId,
            paymentDateLocal.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            requestedBy,
            nowLocal.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            cleanIpAddress,
            safeReason
        });

        var payload = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = requestId,
            ["vnp_Version"] = _configuration["VnPay:Version"] ?? "2.1.0",
            ["vnp_Command"] = "refund",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_TransactionType"] = transactionType,
            ["vnp_TxnRef"] = paymentReference,
            ["vnp_Amount"] = amountMinor.ToString(CultureInfo.InvariantCulture),
            ["vnp_TransactionNo"] = providerTransactionId,
            ["vnp_TransactionDate"] = paymentDateLocal.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CreateBy"] = requestedBy,
            ["vnp_CreateDate"] = nowLocal.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_IpAddr"] = cleanIpAddress,
            ["vnp_OrderInfo"] = safeReason,
            ["vnp_SecureHash"] = VnPayLibrary.HmacSha512(hashSecret, signatureData)
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var response = await _httpClient.PostAsJsonAsync(apiUrl, payload, timeout.Token);
        var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"VNPay refund request failed with HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        string Get(string name) => root.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;

        var expectedResponseData = string.Join('|', new[]
        {
            Get("vnp_ResponseId"), Get("vnp_Command"), Get("vnp_ResponseCode"), Get("vnp_Message"),
            Get("vnp_TmnCode"), Get("vnp_TxnRef"), Get("vnp_Amount"), Get("vnp_BankCode"),
            Get("vnp_PayDate"), Get("vnp_TransactionNo"), Get("vnp_TransactionType"),
            Get("vnp_TransactionStatus"), Get("vnp_OrderInfo"), Get("vnp_PromotionCode"),
            Get("vnp_PromotionAmount")
        });
        var returnedSignature = Get("vnp_SecureHash");
        var calculatedSignature = VnPayLibrary.HmacSha512(hashSecret, expectedResponseData);
        var authenticated = FixedTimeHexEquals(calculatedSignature, returnedSignature) &&
                            Get("vnp_TmnCode") == tmnCode &&
                            Get("vnp_TxnRef") == paymentReference &&
                            long.TryParse(Get("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var returnedAmount) &&
                            returnedAmount == amountMinor;

        var responseCode = Get("vnp_ResponseCode");
        var transactionStatus = Get("vnp_TransactionStatus");
        var status = authenticated && responseCode == "00" && transactionStatus == "00"
            ? "Succeeded"
            : authenticated && (transactionStatus is "05" or "06")
                ? "Pending"
                : authenticated ? "Failed" : "Pending";
        return new ProviderRefundResult(authenticated, status, Get("vnp_TransactionNo"), Get("vnp_Message"));
    }

    private static bool FixedTimeHexEquals(string expectedHex, string actualHex)
    {
        if (string.IsNullOrWhiteSpace(actualHex))
        {
            return false;
        }

        try
        {
            var expected = Convert.FromHexString(expectedHex);
            var actual = Convert.FromHexString(actualHex);
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

using System.Security.Cryptography;
using System.Text;

namespace SportsCenterManagement.BLL.Common.Helpers;

/// <summary>
/// Thư viện tiện ích xử lý các thuật toán bảo mật và chữ ký số cho Cổng thanh toán MoMo (API v2).
/// </summary>
public static class MomoSecurity
{
    /// <summary>
    /// Sinh chữ ký số HMAC-SHA256 cho yêu cầu khởi tạo thanh toán MoMo (captureWallet).
    /// </summary>
    public static string CreateRequestSignature(
        string accessKey,
        long amount,
        string extraData,
        string ipnUrl,
        string orderId,
        string orderInfo,
        string partnerCode,
        string redirectUrl,
        string requestId,
        string requestType,
        string secretKey)
    {
        // Chuỗi dữ liệu thô theo đúng thứ tự alphabet quy định bởi MoMo API v2
        var rawSignature =
            $"accessKey={accessKey}" +
            $"&amount={amount}" +
            $"&extraData={extraData}" +
            $"&ipnUrl={ipnUrl}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&partnerCode={partnerCode}" +
            $"&redirectUrl={redirectUrl}" +
            $"&requestId={requestId}" +
            $"&requestType={requestType}";

        return ComputeHmacSha256(rawSignature, secretKey);
    }

    /// <summary>
    /// Xác thực tính hợp lệ của chữ ký số nhận được từ MoMo Callback/IPN.
    /// </summary>
    public static bool VerifyCallbackSignature(
        IDictionary<string, string> queryParams,
        string accessKey,
        string secretKey)
    {
        queryParams.TryGetValue("partnerCode", out var partnerCode);
        queryParams.TryGetValue("orderId", out var orderId);
        queryParams.TryGetValue("requestId", out var requestId);
        queryParams.TryGetValue("amount", out var rawAmount);
        queryParams.TryGetValue("orderInfo", out var orderInfo);
        queryParams.TryGetValue("orderType", out var orderType);
        queryParams.TryGetValue("transId", out var transId);
        queryParams.TryGetValue("resultCode", out var rawResultCode);
        queryParams.TryGetValue("message", out var message);
        queryParams.TryGetValue("payType", out var payType);
        queryParams.TryGetValue("responseTime", out var responseTime);
        queryParams.TryGetValue("extraData", out var extraData);
        queryParams.TryGetValue("signature", out var signature);

        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        // Chuỗi dữ liệu thô phản hồi từ MoMo
        var rawSignature =
            $"accessKey={accessKey}" +
            $"&amount={rawAmount}" +
            $"&extraData={extraData}" +
            $"&message={message}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&orderType={orderType}" +
            $"&partnerCode={partnerCode}" +
            $"&payType={payType}" +
            $"&requestId={requestId}" +
            $"&responseTime={responseTime}" +
            $"&resultCode={rawResultCode}" +
            $"&transId={transId}";

        var calculatedSignature = ComputeHmacSha256(rawSignature, secretKey);
        var expectedBytes = Encoding.ASCII.GetBytes(calculatedSignature.ToLowerInvariant());
        var actualBytes = Encoding.ASCII.GetBytes(signature.ToLowerInvariant());
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public static string CreateRefundRequestSignature(
        string accessKey,
        long amount,
        string description,
        string orderId,
        string partnerCode,
        string requestId,
        string transactionId,
        string secretKey)
    {
        var rawSignature =
            $"accessKey={accessKey}" +
            $"&amount={amount}" +
            $"&description={description}" +
            $"&orderId={orderId}" +
            $"&partnerCode={partnerCode}" +
            $"&requestId={requestId}" +
            $"&transId={transactionId}";

        return ComputeHmacSha256(rawSignature, secretKey);
    }

    /// <summary>
    /// Băm thông điệp với thuật toán HMAC-SHA256.
    /// </summary>
    public static string ComputeHmacSha256(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

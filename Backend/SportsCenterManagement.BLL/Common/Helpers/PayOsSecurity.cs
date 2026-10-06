using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SportsCenterManagement.BLL.Common.Helpers;

/// <summary>
/// Thư viện bảo mật và tạo/xác thực chữ ký số HMAC-SHA256 theo chuẩn PayOS / VietQR Gateway.
/// </summary>
public static class PayOsSecurity
{
    /// <summary>
    /// Sinh chữ ký số HMAC-SHA256 cho yêu cầu tạo link thanh toán PayOS.
    /// </summary>
    public static string CreateRequestSignature(
        long amount,
        string cancelUrl,
        string description,
        long orderCode,
        string returnUrl,
        string checksumKey)
    {
        var rawData = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
        return ComputeHmacSha256(rawData, checksumKey);
    }

    /// <summary>
    /// Xác thực chữ ký số Webhook nhận được từ PayOS chống giả mạo.
    /// </summary>
    public static bool VerifyWebhookSignature(object? data, string signature, string checksumKey)
    {
        if (data == null || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(checksumKey))
        {
            return false;
        }

        var sortedQueryString = CreateSortedDataQueryString(data);
        var calculatedSignature = ComputeHmacSha256(sortedQueryString, checksumKey);

        return string.Equals(calculatedSignature, signature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sắp xếp các trường của đối tượng Data theo thứ tự Alphabet A-Z.
    /// </summary>
    private static string CreateSortedDataQueryString(object data)
    {
        var json = JsonSerializer.Serialize(data);
        var jsonNode = JsonNode.Parse(json);
        if (jsonNode is not JsonObject jsonObject)
        {
            return string.Empty;
        }

        var sortedDict = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in jsonObject)
        {
            if (property.Value == null) continue;
            sortedDict.Add(property.Key, property.Value.ToString());
        }

        var builder = new StringBuilder();
        foreach (var kv in sortedDict)
        {
            if (builder.Length > 0)
            {
                builder.Append('&');
            }
            builder.Append($"{kv.Key}={kv.Value}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Băm chuỗi dữ liệu với thuật toán HMAC-SHA256.
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

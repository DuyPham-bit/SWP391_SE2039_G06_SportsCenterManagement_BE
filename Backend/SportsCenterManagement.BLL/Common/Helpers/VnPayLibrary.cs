using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SportsCenterManagement.BLL.Common.Helpers;

/// <summary>
/// Thư viện tiện ích xử lý các quy tắc kết nối với cổng thanh toán VNPay:
/// 1. Tự động sắp xếp các tham số theo bảng chữ cái A-Z.
/// 2. Mã hóa URL và băm dữ liệu với HMAC-SHA512 để tạo chữ ký số (vnp_SecureHash).
/// 3. Xác thực tính toàn vẹn chữ ký khi nhận phản hồi IPN / ReturnURL từ VNPay.
/// </summary>
public class VnPayLibrary
{
    private readonly SortedList<string, string> _requestData = new(new VnPayCompare());
    private readonly SortedList<string, string> _responseData = new(new VnPayCompare());

    /// <summary>
    /// Thêm một tham số vào danh sách dữ liệu gửi sang VNPay (bỏ qua nếu giá trị rỗng).
    /// </summary>
    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData.Add(key, value);
        }
    }

    /// <summary>
    /// Thêm một tham số nhận được từ VNPay vào danh sách phản hồi để phục vụ kiểm tra chữ ký.
    /// </summary>
    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData.Add(key, value);
        }
    }

    /// <summary>
    /// Lấy giá trị của một tham số trong danh sách phản hồi từ VNPay theo tên key.
    /// </summary>
    public string GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out var retValue) ? retValue : string.Empty;
    }

    /// <summary>
    /// Tạo URL thanh toán hoàn chỉnh để chuyển hướng người dùng sang cổng VNPay.
    /// </summary>
    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        var data = new StringBuilder();
        foreach (var (key, value) in _requestData)
        {
            data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        var queryString = data.ToString();
        baseUrl += "?" + queryString;
        var signData = queryString.Remove(queryString.Length - 1, 1);
        var vnpSecureHash = HmacSha512(vnpHashSecret, signData);
        baseUrl += "vnp_SecureHash=" + vnpSecureHash;

        return baseUrl;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của chữ ký (vnp_SecureHash) do VNPay gửi về.
    /// </summary>
    public bool ValidateSignature(string inputHash, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(inputHash))
        {
            return false;
        }
        var rspRaw = GetResponseData();
        var myChecksum = HmacSha512(secretKey, rspRaw);
        try
        {
            var expected = Convert.FromHexString(myChecksum);
            var actual = Convert.FromHexString(inputHash);
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Nối toàn bộ dữ liệu phản hồi nhận được từ VNPay thành chuỗi query thô để tính toán chữ ký kiểm tra.
    /// </summary>
    private string GetResponseData()
    {
        var data = new StringBuilder();
        if (_responseData.ContainsKey("vnp_SecureHashType"))
        {
            _responseData.Remove("vnp_SecureHashType");
        }
        if (_responseData.ContainsKey("vnp_SecureHash"))
        {
            _responseData.Remove("vnp_SecureHash");
        }

        foreach (var (key, value) in _responseData)
        {
            data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        if (data.Length > 0)
        {
            data.Remove(data.Length - 1, 1);
        }

        return data.ToString();
    }

    /// <summary>
    /// Hàm mã hóa băm mật mã học HMAC-SHA512.
    /// </summary>
    public static string HmacSha512(string key, string inputData)
    {
        var hash = new StringBuilder();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA512(keyBytes);
        var hashValue = hmac.ComputeHash(inputBytes);
        foreach (var theByte in hashValue)
        {
            hash.Append(theByte.ToString("x2"));
        }

        return hash.ToString();
    }
}

/// <summary>
/// Bộ so sánh chuỗi phục vụ việc sắp xếp Alphabetical (A-Z) theo chuẩn Ordinal của VNPay.
/// </summary>
public class VnPayCompare : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        var vnpCompare = CompareInfo.GetCompareInfo("en-US");
        return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
    }
}

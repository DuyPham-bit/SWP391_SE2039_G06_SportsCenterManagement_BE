namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO chứa dữ liệu Frontend gửi lên khi Member chọn thanh toán một gói tập.
/// </summary>
public class CreatePaymentRequest
{
    /// <summary>
    /// Mã định danh của gói tập (ID từ bảng membership_packages) mà Member chọn mua.
    /// </summary>
    public long PackageId { get; set; }

    /// <summary>
    /// Mã ngân hàng hoặc phương thức thanh toán (tùy chọn, ví dụ: "NCB", "VNBANK", "VNPAYQR").
    /// Nếu để trống, VNPay sẽ hiển thị toàn bộ danh sách ngân hàng để người dùng tự chọn trên cổng.
    /// </summary>
    public string? BankCode { get; set; }
}

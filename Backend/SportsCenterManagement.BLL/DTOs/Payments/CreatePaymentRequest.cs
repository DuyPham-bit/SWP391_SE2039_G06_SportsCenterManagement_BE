using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO chứa dữ liệu Frontend gửi lên khi Member chọn thanh toán một gói tập.
/// </summary>
public class CreatePaymentRequest
{
    /// <summary>
    /// Mã gói tập khi tạo hóa đơn mới; có thể để 0 nếu chỉ tiếp tục thanh toán InvoiceNumber đã có.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long PackageId { get; set; }

    /// <summary>Hóa đơn Pending/PartiallyPaid cần tiếp tục thanh toán khi retry.</summary>
    [MaxLength(50)]
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// Mã ngân hàng hoặc phương thức thanh toán (tùy chọn, ví dụ: "NCB", "VNBANK", "VNPAYQR").
    /// Nếu để trống, VNPay sẽ hiển thị toàn bộ danh sách ngân hàng để người dùng tự chọn trên cổng.
    /// </summary>
    [MaxLength(50)]
    public string? BankCode { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO tiếp nhận yêu cầu thanh toán tại quầy từ Frontend Lễ tân.
/// </summary>
public class CounterPaymentRequest
{
    /// <summary>
    /// ID hội viên được chọn ở Step 1.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng chọn hội viên.")]
    public long MemberId { get; set; }

    /// <summary>
    /// ID gói tập được chọn ở Step 2.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng chọn gói tập.")]
    public long PackageId { get; set; }

    /// <summary>
    /// Phương thức thanh toán được chọn ở Step 3 (CASH, POS, VIETQR, MOMO, VNPAY...).
    /// </summary>
    [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc.")]
    public string PaymentMethod { get; set; } = "CASH";

    /// <summary>
    /// Số tiền thực tế khách đưa vào ô nhập tiền mặt (Dùng để tính tiền thối).
    /// </summary>
    [Range(0, 1_000_000_000, ErrorMessage = "Số tiền không hợp lệ.")]
    public decimal AmountReceived { get; set; }

    /// <summary>
    /// Mã chuẩn chi từ máy POS (nếu quẹt thẻ).
    /// </summary>
    public string? PosApprovalCode { get; set; }

    /// <summary>
    /// Ghi chú thêm của lễ tân.
    /// </summary>
    public string? Note { get; set; }
}

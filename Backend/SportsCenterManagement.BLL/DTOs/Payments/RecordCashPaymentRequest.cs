using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// Yêu cầu ghi nhận thu tiền mặt trực tiếp tại quầy lễ tân.
/// </summary>
/// <param name="Amount">Số tiền thực tế thu từ khách hàng (VNĐ).</param>
/// <param name="IdempotencyKey">Khóa chống trùng lặp giao dịch (Idempotency Key).</param>
public sealed record RecordCashPaymentRequest(
    [property: Range(typeof(decimal), "0.01", "9999999999")] decimal Amount,
    [property: Required, StringLength(100, MinimumLength = 1)] string IdempotencyKey);

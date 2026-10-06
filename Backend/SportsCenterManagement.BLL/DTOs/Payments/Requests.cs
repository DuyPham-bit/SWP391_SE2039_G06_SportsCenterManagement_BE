using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

public static class Requests
{
    /// <summary>Thông tin Frontend gửi lên khi Member chọn thanh toán một gói tập.</summary>
    public sealed class CreatePaymentRequest
    {
        /// <summary>Mã định danh của gói tập mà Member chọn mua.</summary>
        [Range(1, long.MaxValue)]
        public long PackageId { get; set; }

        /// <summary>Mã ngân hàng hoặc phương thức thanh toán; để trống để VNPay hiển thị các lựa chọn.</summary>
        [StringLength(20)]
        public string? BankCode { get; set; }
    }

    public sealed record RecordCashPaymentRequest(
        [property: Range(typeof(decimal), "0.01", "9999999999.99")] decimal Amount,
        [property: Required, StringLength(100, MinimumLength = 16)] string IdempotencyKey);
}

namespace SportsCenterManagement.BLL.DTOs.Payments;

public static class Responses
{
    public sealed record CashPaymentResponse(
        long PaymentId,
        long InvoiceId,
        decimal Amount,
        string Status,
        DateTime PaidAt);

    /// <summary>Kết quả phản hồi sau khi Backend xử lý kết quả thanh toán từ VNPay.</summary>
    public sealed class PaymentResultResponse
    {
        /// <summary>True nếu thanh toán thành công; false nếu thất bại hoặc bị hủy.</summary>
        public bool Success { get; set; }

        /// <summary>Thông điệp mô tả trạng thái giao dịch.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Mã hóa đơn nội bộ của hệ thống.</summary>
        public string? InvoiceNumber { get; set; }

        /// <summary>Mã giao dịch do cổng VNPay sinh ra để đối soát.</summary>
        public string? TransactionId { get; set; }

        /// <summary>Số tiền thực tế của giao dịch (VNĐ).</summary>
        public decimal Amount { get; set; }
    }
}

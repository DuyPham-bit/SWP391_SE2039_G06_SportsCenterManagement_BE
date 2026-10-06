namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO trả về kết quả thanh toán tại quầy, phục vụ Step 4 hiển thị và in biên lai.
/// </summary>
public class CounterPaymentResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Mã giao dịch định danh (Ví dụ: SC-20261002-XXXX). Khớp với receiptData.transactionRef
    /// </summary>
    public string TransactionRef { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceStatus { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền thực tế của gói tập. Khớp với receiptData.amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Số tiền khách đã đưa.
    /// </summary>
    public decimal AmountReceived { get; set; }

    /// <summary>
    /// Số tiền thối lại cho khách.
    /// </summary>
    public decimal ChangeDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingBalance { get; set; }

    /// <summary>
    /// Phương thức thanh toán. Khớp với receiptData.paymentMethod
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm thanh toán thành công (UTC).
    /// </summary>
    public DateTime PaidAt { get; set; }

    /// <summary>
    /// Thông tin hội viên để hiển thị trên biên lai (receiptData.member)
    /// </summary>
    public MemberReceiptDto Member { get; set; } = new();

    /// <summary>
    /// Thông tin gói tập để hiển thị trên biên lai (receiptData.package)
    /// </summary>
    public PackageReceiptDto Package { get; set; } = new();
}

public class MemberReceiptDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string MemberCode { get; set; } = string.Empty;
    public string PackageExpiry { get; set; } = string.Empty; // Chuỗi định dạng dd/MM/yyyy
}

public class PackageReceiptDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationDays { get; set; }
}

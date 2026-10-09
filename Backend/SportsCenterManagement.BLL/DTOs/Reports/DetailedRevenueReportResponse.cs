namespace SportsCenterManagement.BLL.DTOs.Reports;

public sealed class DetailedRevenueReportResponse
{
    public long CenterId { get; init; }

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public string GroupBy { get; init; } = "day";

    public string TimeZone { get; init; } = "Asia/Ho_Chi_Minh";

    public DateTime GeneratedAtUtc { get; init; }

    public decimal GrossRevenue { get; init; }

    public decimal RefundAmount { get; init; }

    public decimal NetRevenue { get; init; }

    public int SuccessfulPaymentCount { get; init; }

    public int RefundedPaymentCount { get; init; }

    public int VoidedPaymentCount { get; init; }

    public int PaidInvoiceCount { get; init; }

    public decimal AverageTicket { get; init; }

    public IReadOnlyList<RevenuePeriodBucketDto> Periods { get; init; } = [];

    public IReadOnlyList<RevenuePaymentMethodDto> PaymentMethods { get; init; } = [];

    public IReadOnlyList<RevenuePackageDto> Packages { get; init; } = [];

    public IReadOnlyList<RevenueTransactionDto> Transactions { get; init; } = [];
}

public sealed record RevenuePeriodBucketDto(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal GrossRevenue,
    decimal RefundAmount,
    decimal NetRevenue,
    int SuccessfulPaymentCount,
    int RefundedPaymentCount);

public sealed record RevenuePaymentMethodDto(
    string PaymentMethod,
    decimal GrossRevenue,
    int PaymentCount);

public sealed record RevenuePackageDto(
    long PackageId,
    string PackageName,
    int SoldCount,
    decimal Revenue);

public sealed record RevenueTransactionDto(
    long PaymentId,
    string InvoiceNumber,
    DateTime PaidAtUtc,
    string PaymentStatus,
    string PaymentMethod,
    string? TransactionCode,
    decimal Amount,
    long MemberId,
    string MemberCode,
    string MemberName,
    long? ProcessedBy,
    long? RefundId = null);

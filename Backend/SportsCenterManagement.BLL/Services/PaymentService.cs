using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using SportsCenterManagement.BLL.Common.Helpers;
using PaymentsRequests = SportsCenterManagement.BLL.DTOs.Payments.Requests;
using PaymentsResponses = SportsCenterManagement.BLL.DTOs.Payments.Responses;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Service cài đặt các nghiệp vụ thanh toán gói tập qua VNPay.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public PaymentService(IUnitOfWork unitOfWork, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    /// <summary>
    /// 1. Tạo bản ghi đăng ký gói tập và hóa đơn với trạng thái chờ (PendingPayment/Issued).
    /// 2. Đóng gói các tham số và ký mã SHA512 để sinh URL VNPay.
    /// </summary>
    public async Task<string> CreatePaymentUrlAsync(
        long userId,
        PaymentsRequests.CreatePaymentRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || request.PackageId <= 0)
        {
            throw new InvalidOperationException("Thông tin thành viên hoặc gói tập không hợp lệ.");
        }

        var tmnCode = _configuration["VnPay:TmnCode"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:TmnCode");
        var hashSecret = _configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:HashSecret");
        var baseUrl = _configuration["VnPay:BaseUrl"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:BaseUrl");
        var returnUrl = _configuration["VnPay:ReturnUrl"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:ReturnUrl");

        // Claims chứa UserId; chuyển qua profile server-side để không tin member id từ client.
        var member = await _unitOfWork.Repository<MemberProfile>()
            .Find(profile => profile.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy thông tin thành viên.");

        // Dùng serializable để request retry song song cùng tái sử dụng invoice chờ hiện có.
        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var now = DateTime.UtcNow;
        var db = _unitOfWork.Context;
        var pending = await (
            from item in db.InvoiceItems
            join invoiceRow in db.Invoices on item.InvoiceId equals invoiceRow.Id
            join subscriptionRow in db.MemberSubscriptions on item.SubscriptionId equals subscriptionRow.Id
            join packageRow in db.MembershipPackages on item.PackageId equals (long?)packageRow.Id
            where invoiceRow.MemberId == member.Id
                  && (invoiceRow.Status == "Issued" || invoiceRow.Status == "PartiallyPaid")
                  && item.PackageId == request.PackageId
                  && subscriptionRow.Status == "PendingPayment"
            orderby invoiceRow.IssuedAt descending
            select new { Invoice = invoiceRow, Package = packageRow })
            .FirstOrDefaultAsync(cancellationToken);

        Invoice invoice;
        MembershipPackage package;
        if (pending is not null)
        {
            invoice = pending.Invoice;
            package = pending.Package;
        }
        else
        {
            package = await _unitOfWork.Repository<MembershipPackage>()
                .Find(item => item.Id == request.PackageId && item.Status == "Active")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Gói tập không tồn tại hoặc đã ngừng hoạt động.");

            if (package.Price <= 0 || package.DurationDays <= 0)
            {
                throw new InvalidOperationException("Gói tập có giá hoặc thời hạn không hợp lệ.");
            }

            var subscription = new MemberSubscription
            {
                MemberId = member.Id,
                PackageId = package.Id,
                StartDate = null,
                EndDate = null,
                DurationDays = package.DurationDays,
                Price = package.Price,
                Status = "PendingPayment",
                AutoRenew = false,
                CreatedAt = now
            };
            await _unitOfWork.Repository<MemberSubscription>().AddAsync(subscription, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var newInvoiceNumber = $"SC-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30];
            invoice = new Invoice
            {
                InvoiceNumber = newInvoiceNumber,
                MemberId = member.Id,
                CenterId = package.CenterId,
                Subtotal = package.Price,
                Discount = 0,
                Tax = 0,
                TotalAmount = package.Price,
                Status = "Issued",
                IssuedAt = now
            };
            await _unitOfWork.Repository<Invoice>().AddAsync(invoice, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.Repository<InvoiceItem>().AddAsync(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                PackageId = package.Id,
                SubscriptionId = subscription.Id,
                Description = $"Thanh toán gói tập: {package.Name}",
                Quantity = 1,
                UnitPrice = package.Price,
                Amount = package.Price
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (member.CenterId.HasValue && member.CenterId.Value != package.CenterId)
        {
            throw new UnauthorizedAccessException("Gói tập không thuộc trung tâm của thành viên.");
        }
        if (!member.CenterId.HasValue)
        {
            member.CenterId = package.CenterId;
            member.UpdatedAt = now;
            _unitOfWork.Repository<MemberProfile>().Update(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var invoiceNumber = invoice.InvoiceNumber;

        var vnpay = new VnPayLibrary();
        var currencyCode = _configuration["VnPay:CurrCode"] ?? "VND";
        if (!string.Equals(currencyCode, "VND", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Flow thanh toán hiện chỉ hỗ trợ VND.");
        }

        var requestReference = Guid.NewGuid().ToString("N");
        var paidAmount = await _unitOfWork.Repository<Payment>()
            .Find(payment => payment.InvoiceId == invoice.Id && payment.PaymentStatus == "Succeeded")
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var outstandingAmount = invoice.TotalAmount - paidAmount;
        if (outstandingAmount <= 0)
        {
            throw new InvalidOperationException("Hóa đơn không còn số dư cần thanh toán.");
        }
        var amountInVnpayFormat = decimal.ToInt64(outstandingAmount * 100m)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        var clientIp = (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1" || ipAddress.Contains(':'))
            ? "127.0.0.1"
            : ipAddress;

        vnpay.AddRequestData("vnp_Version", _configuration["VnPay:Version"] ?? "2.1.0");
        vnpay.AddRequestData("vnp_Command", _configuration["VnPay:Command"] ?? "pay");
        vnpay.AddRequestData("vnp_TmnCode", tmnCode);
        vnpay.AddRequestData("vnp_Amount", amountInVnpayFormat);
        vnpay.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", currencyCode);
        vnpay.AddRequestData("vnp_IpAddr", clientIp);
        vnpay.AddRequestData("vnp_Locale", _configuration["VnPay:Locale"] ?? "vn");
        vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan goi tap {package.Id} - Don hang {invoiceNumber}");
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnpay.AddRequestData("vnp_TxnRef", requestReference);


        // Nếu người dùng có chọn ngân hàng cụ thể từ Frontend
        if (!string.IsNullOrEmpty(request.BankCode))
        {
            vnpay.AddRequestData("vnp_BankCode", request.BankCode);
        }

        // Tạo URL trước khi commit để không lưu attempt nếu không thể tạo yêu cầu gửi tới VNPay.
        var paymentUrl = vnpay.CreateRequestUrl(baseUrl, hashSecret);
        var paymentAttempt = new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = member.Id,
            PaymentMethod = "VNPAY",
            TransactionCode = $"VNPAY-REQ-{requestReference}",
            ProviderReference = requestReference,
            Amount = outstandingAmount,
            PaymentStatus = "Pending",
            AttemptedAt = now,
            Note = "Awaiting VNPay callback."
        };
        await _unitOfWork.Repository<Payment>().AddAsync(paymentAttempt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(_unitOfWork, userId, package.CenterId, "payment.vnpay.started", "Payment", paymentAttempt.Id,
            newValues: new { InvoiceId = invoice.Id, Amount = outstandingAmount, ProviderReference = requestReference });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return paymentUrl;
    }

    /// <summary>
    /// Xử lý dữ liệu trả về sau khi người dùng thực hiện thanh toán trên VNPay:
    /// - Kiểm tra signature hợp lệ để chống giả mạo.
    /// - Cập nhật trạng thái Invoice -> Paid và Subscription -> Active nếu thanh toán thành công (Mã 00).
    /// </summary>
    public async Task<PaymentsResponses.PaymentResultResponse> ProcessPaymentCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
        var vnpay = new VnPayLibrary();
        foreach (var (key, value) in queryParams)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
            {
                vnpay.AddResponseData(key, value);
            }
        }

        var hashSecret = _configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:HashSecret");
        if (!queryParams.TryGetValue("vnp_SecureHash", out var vnpSecureHash) || string.IsNullOrEmpty(vnpSecureHash))
        {
            return new PaymentsResponses.PaymentResultResponse
            {
                Success = false,
                Message = "Thiếu chữ ký bảo mật từ VNPay."
            };
        }

        // 1. Kiểm tra signature bảo mật từ VNPay
        var isValidSignature = vnpay.ValidateSignature(vnpSecureHash, hashSecret);
        if (!isValidSignature)
        {
            return new PaymentsResponses.PaymentResultResponse
            {
                Success = false,
                Message = "signature bảo mật không hợp lệ (Dữ liệu có thể đã bị can thiệp)."
            };
        }

        var invoiceNumber = vnpay.GetResponseData("vnp_TxnRef");
        var vnpResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
        var vnpTransactionNo = vnpay.GetResponseData("vnp_TransactionNo");
        if (string.IsNullOrWhiteSpace(invoiceNumber)
            || !decimal.TryParse(vnpay.GetResponseData("vnp_Amount"), out var amountInVnpayFormat)
            || amountInVnpayFormat <= 0)
        {
            AuditLogWriter.Add(_unitOfWork, null, null, "payment.vnpay.invalid-payload", "Invoice", null,
                new
                {
                    ProviderReference = invoiceNumber,
                    Amount = vnpay.GetResponseData("vnp_Amount"),
                    ResponseCode = vnpResponseCode
                });
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PaymentsResponses.PaymentResultResponse { Success = false, Message = "Dữ liệu thanh toán không hợp lệ." };
        }

        var vnpAmount = amountInVnpayFormat / 100m;
        var transactionCode = CreateCallbackTransactionCode(queryParams, vnpTransactionNo);
        var currencyCode = vnpay.GetResponseData("vnp_CurrCode");
        // Serializable tránh hai callback đồng thời cùng cộng một giao dịch và kích hoạt hai lần.
        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var paymentAttempt = await _unitOfWork.Context.Payments
            .SingleOrDefaultAsync(payment => payment.ProviderReference == invoiceNumber, cancellationToken);
        var invoice = paymentAttempt is null
            ? await _unitOfWork.Repository<Invoice>()
                .Find(item => item.InvoiceNumber == invoiceNumber)
                .SingleOrDefaultAsync(cancellationToken)
            : await _unitOfWork.Repository<Invoice>().GetByIdAsync(paymentAttempt.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            AuditLogWriter.Add(_unitOfWork, null, null, "payment.vnpay.invoice-not-found", "Invoice", null,
                newValues: new { ProviderReference = invoiceNumber, TransactionCode = transactionCode, Amount = vnpAmount });
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentsResponses.PaymentResultResponse { Success = false, Message = "Không tìm thấy hóa đơn tương ứng với giao dịch." };
        }

        var existingPayment = await _unitOfWork.Repository<Payment>()
            .Find(item => item.TransactionCode == transactionCode)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingPayment is not null)
        {
            var sameInvoice = existingPayment.InvoiceId == invoice.Id;
            var sameAmount = existingPayment.Amount == vnpAmount;
            if (!sameInvoice || !sameAmount)
            {
                AuditLogWriter.Add(_unitOfWork, null, invoice.CenterId, "payment.vnpay.transaction-conflict", "Payment", existingPayment.Id,
                    newValues: new { IncomingInvoiceId = invoice.Id, IncomingAmount = vnpAmount });
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new PaymentsResponses.PaymentResultResponse
            {
                Success = sameInvoice && sameAmount && existingPayment.PaymentStatus == "Succeeded",
                Message = !sameInvoice ? "Mã giao dịch đã được sử dụng cho hóa đơn khác."
                    : !sameAmount ? "Mã giao dịch đã được xử lý với số tiền khác."
                    : "Giao dịch đã được xử lý.",
                InvoiceNumber = invoice.InvoiceNumber,
                TransactionId = vnpTransactionNo,
                Amount = existingPayment.Amount
            };
        }

        if (paymentAttempt is not null && paymentAttempt.PaymentStatus != "Pending")
        {
            var sameAmount = paymentAttempt.Amount == vnpAmount;
            if (!sameAmount)
            {
                AuditLogWriter.Add(_unitOfWork, null, invoice.CenterId,
                    "payment.vnpay.attempt-conflict", "Payment", paymentAttempt.Id,
                    newValues: new { IncomingAmount = vnpAmount, RecordedAmount = paymentAttempt.Amount });
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new PaymentsResponses.PaymentResultResponse
            {
                Success = sameAmount && paymentAttempt.PaymentStatus == "Succeeded",
                Message = sameAmount ? "Yêu cầu thanh toán này đã được xử lý." : "Mã yêu cầu thanh toán đã được xử lý với số tiền khác.",
                InvoiceNumber = invoice.InvoiceNumber,
                TransactionId = vnpTransactionNo,
                Amount = paymentAttempt.Amount
            };
        }

        if (invoice.Status is "Paid" or "Cancelled" or "Voided" or "Refunded")
        {
            var gatewaySucceeded = vnpResponseCode == "00";
            var now = DateTime.UtcNow;
            await RecordCallbackAttemptAsync(invoice, transactionCode, vnpAmount, now,
                gatewaySucceeded ? "ReviewRequired" : "Failed",
                gatewaySucceeded ? "payment.vnpay.review-required" : "payment.vnpay.failed",
                gatewaySucceeded
                    ? $"VNPay báo thành công nhưng hóa đơn ở trạng thái {invoice.Status}; cần đối soát/refund."
                    : $"VNPay response code: {vnpResponseCode}; invoice status: {invoice.Status}.",
                cancellationToken, paymentAttempt);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentsResponses.PaymentResultResponse
            {
                Success = false,
                Message = invoice.Status == "Paid" ? "Hóa đơn đã được thanh toán bằng giao dịch khác." : "Hóa đơn đã bị hủy.",
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = vnpAmount
            };
        }

        var invoiceItems = await _unitOfWork.Repository<InvoiceItem>()
            .Find(item => item.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        if (!string.Equals(currencyCode, "VND", StringComparison.Ordinal))
        {
            var now = DateTime.UtcNow;
            var gatewaySucceeded = vnpResponseCode == "00";
            await RecordCallbackAttemptAsync(invoice, transactionCode, vnpAmount, now,
                gatewaySucceeded ? "ReviewRequired" : "Failed",
                gatewaySucceeded ? "payment.vnpay.review-required" : "payment.vnpay.failed",
                $"Unexpected VNPay currency: {currencyCode}; response code: {vnpResponseCode}.", cancellationToken,
                paymentAttempt);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentsResponses.PaymentResultResponse
            {
                Success = false,
                Message = "Đơn vị tiền tệ thanh toán không hợp lệ.",
                InvoiceNumber = invoice.InvoiceNumber,
                TransactionId = vnpTransactionNo,
                Amount = vnpAmount
            };
        }

        if (vnpResponseCode == "00")
        {
            if (string.IsNullOrWhiteSpace(vnpTransactionNo))
            {
                await RecordCallbackAttemptAsync(invoice, transactionCode, vnpAmount, DateTime.UtcNow,
                    "ReviewRequired", "payment.vnpay.review-required",
                    "VNPay reported success without a transaction number.", cancellationToken, paymentAttempt);
                await transaction.CommitAsync(cancellationToken);
                return new PaymentsResponses.PaymentResultResponse
                {
                    Success = false,
                    Message = "Thiếu mã giao dịch thanh toán.",
                    InvoiceNumber = invoice.InvoiceNumber,
                    Amount = vnpAmount
                };
            }

            if (paymentAttempt is not null && paymentAttempt.Amount != vnpAmount)
            {
                await RecordCallbackAttemptAsync(invoice, transactionCode, vnpAmount, DateTime.UtcNow,
                    "ReviewRequired", "payment.vnpay.review-required",
                    "VNPay reported success for an amount different from the amount requested for this payment attempt.",
                    cancellationToken, paymentAttempt);
                await transaction.CommitAsync(cancellationToken);
                return new PaymentsResponses.PaymentResultResponse
                {
                    Success = false,
                    Message = "Số tiền callback không khớp với yêu cầu thanh toán; cần đối soát.",
                    InvoiceNumber = invoice.InvoiceNumber,
                    TransactionId = vnpTransactionNo,
                    Amount = vnpAmount
                };
            }

            var payments = await _unitOfWork.Repository<Payment>()
                .Find(item => item.InvoiceId == invoice.Id && item.PaymentStatus == "Succeeded")
                .ToListAsync(cancellationToken);
            var paidBefore = payments.Sum(item => item.Amount);
            var outstanding = invoice.TotalAmount - paidBefore;

            if (vnpAmount > outstanding || outstanding <= 0)
            {
                await RecordCallbackAttemptAsync(invoice, transactionCode, vnpAmount, DateTime.UtcNow,
                    "ReviewRequired", "payment.vnpay.review-required",
                    "VNPay reported success for an amount above the outstanding invoice balance.", cancellationToken,
                    paymentAttempt);
                await transaction.CommitAsync(cancellationToken);
                return new PaymentsResponses.PaymentResultResponse
                {
                    Success = false,
                    Message = "Số tiền thanh toán vượt quá số dư hóa đơn.",
                    InvoiceNumber = invoice.InvoiceNumber,
                    TransactionId = vnpTransactionNo,
                    Amount = vnpAmount
                };
            }

            var now = DateTime.UtcNow;
            var payment = paymentAttempt ?? new Payment
            {
                InvoiceId = invoice.Id,
                MemberId = invoice.MemberId,
                PaymentMethod = "VNPAY"
            };
            payment.TransactionCode = transactionCode;
            payment.Amount = vnpAmount;
            payment.PaymentStatus = "Succeeded";
            payment.PaidAt = now;
            payment.AttemptedAt = now;
            payment.Note = "Thanh toán qua cổng VNPay Sandbox";
            if (paymentAttempt is null)
            {
                await _unitOfWork.Repository<Payment>().AddAsync(payment, cancellationToken);
            }
            else
            {
                _unitOfWork.Repository<Payment>().Update(payment);
            }

            var paidTotal = paidBefore + vnpAmount;
            invoice.Status = paidTotal == invoice.TotalAmount ? "Paid" : "PartiallyPaid";
            invoice.PaidAt = invoice.Status == "Paid" ? now : null;
            _unitOfWork.Repository<Invoice>().Update(invoice);

            if (invoice.Status == "Paid")
            {
                foreach (var subscriptionId in invoiceItems
                             .Where(item => item.SubscriptionId.HasValue)
                             .Select(item => item.SubscriptionId!.Value)
                             .Distinct())
                {
                    var subscription = await _unitOfWork.Repository<MemberSubscription>()
                        .GetByIdAsync(subscriptionId, cancellationToken);
                    if (subscription is null || subscription.Status != "PendingPayment")
                    {
                        continue;
                    }

                    if (subscription.DurationDays <= 0)
                    {
                        throw new InvalidOperationException("Không thể kích hoạt subscription vì thời hạn snapshot không hợp lệ.");
                    }

                    var today = DateOnly.FromDateTime(now);
                    var currentEnd = await _unitOfWork.Repository<MemberSubscription>()
                        .Find(item => item.MemberId == subscription.MemberId
                                      && item.Status == "Active"
                                      && item.EndDate >= today)
                        .MaxAsync(item => item.EndDate, cancellationToken);
                    // Gia hạn nối tiếp subscription đang còn hạn; ngày kết thúc được tính inclusive.
                    var startDate = currentEnd.HasValue ? currentEnd.Value.AddDays(1) : today;
                    subscription.StartDate = startDate;
                    subscription.EndDate = startDate.AddDays(subscription.DurationDays - 1);
                    subscription.Status = "Active";
                    subscription.UpdatedAt = now;
                    _unitOfWork.Repository<MemberSubscription>().Update(subscription);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            AuditLogWriter.Add(_unitOfWork, null, invoice.CenterId, "payment.vnpay.succeeded", "Payment", payment.Id,
                newValues: new { InvoiceId = invoice.Id, Amount = vnpAmount, Currency = currencyCode });
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PaymentsResponses.PaymentResultResponse
            {
                Success = true,
                Message = invoice.Status == "Paid" ? "Thanh toán đủ hóa đơn thành công." : "Đã ghi nhận thanh toán một phần; subscription vẫn chờ thanh toán.",
                InvoiceNumber = invoice.InvoiceNumber,
                TransactionId = vnpTransactionNo,
                Amount = vnpAmount
            };
        }

        var failureAt = DateTime.UtcNow;
        await RecordCallbackAttemptAsync(invoice, transactionCode, vnpAmount, failureAt,
            "Failed", "payment.vnpay.failed", $"VNPay response code: {vnpResponseCode}", cancellationToken,
            paymentAttempt);
        await transaction.CommitAsync(cancellationToken);
        return new PaymentsResponses.PaymentResultResponse
        {
            Success = false,
            Message = $"Thanh toán không thành công. Mã lỗi VNPay: {vnpResponseCode}",
            InvoiceNumber = invoice.InvoiceNumber,
            TransactionId = vnpTransactionNo,
            Amount = vnpAmount
        };
    }

    private async Task RecordCallbackAttemptAsync(
        Invoice invoice,
        string transactionCode,
        decimal amount,
        DateTime attemptedAt,
        string paymentStatus,
        string auditAction,
        string note,
        CancellationToken cancellationToken,
        Payment? paymentAttempt = null)
    {
        var payment = paymentAttempt ?? new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = invoice.MemberId,
            PaymentMethod = "VNPAY"
        };
        payment.TransactionCode = transactionCode;
        payment.Amount = amount;
        payment.PaymentStatus = paymentStatus;
        payment.PaidAt = null;
        payment.AttemptedAt = attemptedAt;
        payment.Note = note.Length <= 500 ? note : note[..500];
        if (paymentAttempt is null)
        {
            await _unitOfWork.Repository<Payment>().AddAsync(payment, cancellationToken);
        }
        else
        {
            _unitOfWork.Repository<Payment>().Update(payment);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(_unitOfWork, null, invoice.CenterId, auditAction, "Payment", payment.Id,
            newValues: new { InvoiceId = invoice.Id, Amount = amount, Reason = payment.Note });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string CreateCallbackTransactionCode(
        IDictionary<string, string> queryParams,
        string? transactionNumber)
    {
        if (!string.IsNullOrWhiteSpace(transactionNumber))
        {
            var normalizedTransactionNumber = transactionNumber.Trim();
            if (normalizedTransactionNumber.Length <= 140)
            {
                return $"VNPAY-{normalizedTransactionNumber}";
            }
        }

        var canonical = string.Join("&", queryParams
            .Where(pair => pair.Key.StartsWith("vnp_", StringComparison.Ordinal)
                           && pair.Key is not "vnp_SecureHash" and not "vnp_SecureHashType")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"VNPAY-{hash}";
    }
}

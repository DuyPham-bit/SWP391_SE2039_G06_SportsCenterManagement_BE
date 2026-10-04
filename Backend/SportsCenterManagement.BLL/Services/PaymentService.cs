using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Service điều phối nghiệp vụ thanh toán của Trung tâm Thể thao:
/// - Kiểm tra điều kiện mua gói của Member.
/// - Khởi tạo hóa đơn và gói tập chờ thanh toán trong Database.
/// - Ủy quyền cho IVnPayService / IMoMoService tạo URL thanh toán.
/// - Cập nhật trạng thái kích hoạt gói tập khi có kết quả callback.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVnPayService _vnPayService;
    private readonly IMoMoService _moMoService;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IVnPayService vnPayService,
        IMoMoService moMoService)
    {
        _unitOfWork = unitOfWork;
        _vnPayService = vnPayService;
        _moMoService = moMoService;
    }

    #region VNPay Implementation

    public async Task<string> CreatePaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var (invoiceNumber, package) = await CreatePendingInvoiceAndSubscriptionAsync(memberId, request.PackageId, cancellationToken);
        var orderDescription = $"Thanh toan goi tap {package.Id} - Don hang {invoiceNumber}";
        return _vnPayService.CreatePaymentUrl(invoiceNumber, package.Price, orderDescription, ipAddress, request.BankCode);
    }

    public async Task<PaymentResultResponse> ProcessPaymentCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
        var callbackResult = _vnPayService.ProcessCallback(queryParams);

        if (!callbackResult.IsValidSignature)
        {
            return new PaymentResultResponse
            {
                Success = false,
                Message = "Chữ ký bảo mật VNPay không hợp lệ."
            };
        }

        return await CompletePaymentTransactionAsync(
            invoiceNumber: callbackResult.InvoiceNumber,
            isSuccess: callbackResult.IsSuccess,
            paymentMethod: string.IsNullOrEmpty(callbackResult.BankCode) ? "VNPAY" : $"VNPAY-{callbackResult.BankCode}",
            transactionId: callbackResult.TransactionNo,
            amount: callbackResult.Amount,
            gatewayNote: "Thanh toán qua cổng VNPay Sandbox",
            responseCodeMessage: $"Mã phản hồi VNPay: {callbackResult.ResponseCode}",
            cancellationToken: cancellationToken);
    }

    #endregion

    #region MoMo Implementation

    public async Task<string> CreateMomoPaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var (invoiceNumber, package) = await CreatePendingInvoiceAndSubscriptionAsync(memberId, request.PackageId, cancellationToken);
        var orderInfo = $"Thanh toan goi tap {package.Id} - Don hang {invoiceNumber}";

        var momoResponse = await _moMoService.CreatePaymentUrlAsync(invoiceNumber, package.Price, orderInfo, cancellationToken);

        if (momoResponse.ResultCode != 0)
        {
            throw new InvalidOperationException($"Không thể tạo link MoMo: {momoResponse.Message} (Mã lỗi: {momoResponse.ResultCode})");
        }

        return momoResponse.PayUrl;
    }

    public async Task<PaymentResultResponse> ProcessMomoCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
        var callbackResult = _moMoService.ProcessCallback(queryParams);

        if (!callbackResult.IsValidSignature)
        {
            return new PaymentResultResponse
            {
                Success = false,
                Message = "Chữ ký bảo mật MoMo không hợp lệ (Dữ liệu có thể đã bị can thiệp)."
            };
        }

        return await CompletePaymentTransactionAsync(
            invoiceNumber: callbackResult.OrderId,
            isSuccess: callbackResult.IsSuccess,
            paymentMethod: string.IsNullOrEmpty(callbackResult.PayType) ? "MOMO" : $"MOMO-{callbackResult.PayType.ToUpper()}",
            transactionId: callbackResult.TransId,
            amount: callbackResult.Amount,
            gatewayNote: "Thanh toán qua cổng MoMo Sandbox",
            responseCodeMessage: $"Mã phản hồi MoMo: {callbackResult.ResultCode} - {callbackResult.Message}",
            cancellationToken: cancellationToken);
    }

    #endregion



    #region Counter / Cash Payment Implementation

    public async Task<CounterPaymentResponse> ProcessCounterPaymentAsync(
        long staffUserId,
        CounterPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra hội viên tồn tại
        var member = await _unitOfWork.Repository<MemberProfile>()
            .GetByIdAsync(request.MemberId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy thông tin hội viên.");

        // 2. Kiểm tra gói tập đang hoạt động và lấy giá gốc từ DB
        var package = await _unitOfWork.Repository<MembershipPackage>()
            .Find(p => p.Id == request.PackageId && p.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Gói tập không tồn tại hoặc đã ngừng hoạt động.");

        var totalAmount = package.Price;
        var amountReceived = request.AmountReceived > 0 ? request.AmountReceived : totalAmount;

        // Kiểm tra số tiền khách đưa nếu thanh toán tiền mặt
        if (request.PaymentMethod.Contains("CASH", StringComparison.OrdinalIgnoreCase) ||
            request.PaymentMethod.Contains("TIỀN MẶT", StringComparison.OrdinalIgnoreCase))
        {
            if (amountReceived < totalAmount)
            {
                throw new InvalidOperationException($"Số tiền khách đưa ({amountReceived:N0}đ) không đủ để thanh toán gói tập ({totalAmount:N0}đ).");
            }
        }

        var changeDue = Math.Max(0, amountReceived - totalAmount);

        // 3. Mở Transaction bảo đảm tính toàn vẹn
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var expiryDate = today.AddDays(package.DurationDays);

        // A. Tạo gói tập kích hoạt ngay lập tức
        var subscription = new MemberSubscription
        {
            MemberId = request.MemberId,
            PackageId = package.Id,
            StartDate = today,
            EndDate = expiryDate,
            Price = totalAmount,
            Status = "Active",
            AutoRenew = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _unitOfWork.Repository<MemberSubscription>().AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // B. Tạo Hóa Đơn (Invoice)
        var invoiceNumber = $"SC-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30];
        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            MemberId = request.MemberId,
            CenterId = package.CenterId,
            CreatedBy = staffUserId,
            Subtotal = totalAmount,
            Discount = 0,
            Tax = 0,
            TotalAmount = totalAmount,
            Status = "Paid",
            IssuedAt = now,
            PaidAt = now
        };
        await _unitOfWork.Repository<Invoice>().AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // C. Tạo Chi Tiết Hóa Đơn (InvoiceItem)
        var invoiceItem = new InvoiceItem
        {
            InvoiceId = invoice.Id,
            PackageId = package.Id,
            SubscriptionId = subscription.Id,
            Description = $"Thanh toán tại quầy gói tập: {package.Name}",
            Quantity = 1,
            UnitPrice = totalAmount,
            Amount = totalAmount
        };
        await _unitOfWork.Repository<InvoiceItem>().AddAsync(invoiceItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // D. Tạo Giao Dịch Thanh Toán (Payment)
        var transactionCode = !string.IsNullOrWhiteSpace(request.PosApprovalCode)
            ? request.PosApprovalCode
            : $"{request.PaymentMethod.ToUpper()}-{invoiceNumber}";

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Thanh toán tại quầy ({request.PaymentMethod}) do Staff ID: {staffUserId} xử lý"
            : $"{request.Note} (Staff ID: {staffUserId})";

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = request.MemberId,
            ProcessedBy = staffUserId,
            PaymentMethod = request.PaymentMethod,
            TransactionCode = transactionCode,
            Amount = totalAmount,
            PaymentStatus = "Completed",
            PaidAt = now,
            Note = note
        };
        await _unitOfWork.Repository<Payment>().AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Hoàn tất Commit Transaction
        await transaction.CommitAsync(cancellationToken);

        // 4. Trả về kết quả khớp với receiptData của Frontend
        return new CounterPaymentResponse
        {
            Success = true,
            Message = "Thanh toán và kích hoạt gói tập tại quầy thành công!",
            TransactionRef = invoiceNumber,
            Amount = totalAmount,
            AmountReceived = amountReceived,
            ChangeDue = changeDue,
            PaymentMethod = request.PaymentMethod,
            PaidAt = now,
            Member = new MemberReceiptDto
            {
                Id = member.Id,
                FullName = member.FullName,
                MemberCode = member.MemberCode,
                PackageExpiry = expiryDate.ToString("dd/MM/yyyy")
            },
            Package = new PackageReceiptDto
            {
                Id = package.Id,
                Name = package.Name,
                DurationDays = package.DurationDays
            }
        };
    }

    public async Task<PaymentResultResponse> VoidCounterPaymentAsync(
     long staffUserId,
     string invoiceNumber,
     string reason,
     CancellationToken cancellationToken = default)
    {
        var invoice = await _unitOfWork.Repository<Invoice>()
            .Find(i => i.InvoiceNumber == invoiceNumber)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy hóa đơn cần hủy.");

        if (invoice.Status != "Paid")
        {
            throw new InvalidOperationException($"Chỉ có thể hủy hóa đơn đã thanh toán. Trạng thái hiện tại: {invoice.Status}");
        }

        var now = DateTime.UtcNow;

        // 🛡️ TẦNG BẢO VỆ CHỐNG LẠM DỤNG: Chỉ cho phép tự hủy trong vòng 15 phút kể từ lúc thanh toán
        var paidTime = invoice.PaidAt ?? invoice.IssuedAt;
        var elapsedMinutes = (now - paidTime).TotalMinutes;

        if (elapsedMinutes > 15)
        {
            throw new InvalidOperationException(
                $"Giao dịch đã thực hiện cách đây {Math.Round(elapsedMinutes)} phút (quá thời hạn 15 phút tự hủy). " +
                "Vui lòng liên hệ Quản lý (Manager) để phê duyệt hủy hóa đơn!");
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // 1. Chuyển trạng thái Hóa đơn sang Cancelled
        invoice.Status = "Cancelled";
        _unitOfWork.Repository<Invoice>().Update(invoice);

        // 2. Thu hồi gói tập ngay lập tức (Chuyển sang Cancelled để không thể check-in)
        var invoiceItem = await _unitOfWork.Repository<InvoiceItem>()
            .Find(item => item.InvoiceId == invoice.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (invoiceItem?.SubscriptionId != null)
        {
            var subscription = await _unitOfWork.Repository<MemberSubscription>()
                .GetByIdAsync(invoiceItem.SubscriptionId.Value, cancellationToken);
            if (subscription != null)
            {
                subscription.Status = "Cancelled";
                subscription.UpdatedAt = now;
                _unitOfWork.Repository<MemberSubscription>().Update(subscription);
            }
        }

        // 3. Đánh dấu giao dịch thanh toán bị Voided và lưu vết Audit Log
        var payments = await _unitOfWork.Repository<Payment>()
            .Find(p => p.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        foreach (var p in payments)
        {
            p.PaymentStatus = "Voided";
            p.RefundApprovedBy = staffUserId;
            p.Note = $"{p.Note} | [HỦY GIAO DỊCH NHẦM lúc {now:dd/MM/yyyy HH:mm:ss} bởi Staff ID {staffUserId}. Lý do: {reason}]";
            _unitOfWork.Repository<Payment>().Update(p);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PaymentResultResponse
        {
            Success = true,
            Message = $"Đã hủy thành công giao dịch hóa đơn {invoiceNumber} và thu hồi gói tập.",
            InvoiceNumber = invoiceNumber,
            Amount = invoice.TotalAmount
        };
    }


    #endregion


    #region Private Helper Methods (Dùng chung)

    private async Task<(string InvoiceNumber, MembershipPackage Package)> CreatePendingInvoiceAndSubscriptionAsync(
        long memberId,
        long packageId,
        CancellationToken cancellationToken)
    {
        var memberExists = await _unitOfWork.Repository<MemberProfile>()
            .AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            throw new InvalidOperationException("Không tìm thấy thông tin thành viên.");
        }

        var package = await _unitOfWork.Repository<MembershipPackage>()
            .Find(p => p.Id == packageId && p.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Gói tập không tồn tại hoặc đã ngừng hoạt động.");

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var subscription = new MemberSubscription
        {
            MemberId = memberId,
            PackageId = package.Id,
            StartDate = today,
            EndDate = today.AddDays(package.DurationDays),
            Price = package.Price,
            Status = "PendingPayment",
            AutoRenew = false,
            CreatedAt = now
        };
        await _unitOfWork.Repository<MemberSubscription>().AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var invoiceNumber = $"SC-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30];
        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            MemberId = memberId,
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

        var invoiceItem = new InvoiceItem
        {
            InvoiceId = invoice.Id,
            PackageId = package.Id,
            SubscriptionId = subscription.Id,
            Description = $"Thanh toán gói tập: {package.Name}",
            Quantity = 1,
            UnitPrice = package.Price,
            Amount = package.Price
        };
        await _unitOfWork.Repository<InvoiceItem>().AddAsync(invoiceItem, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return (invoiceNumber, package);
    }

    private async Task<PaymentResultResponse> CompletePaymentTransactionAsync(
        string invoiceNumber,
        bool isSuccess,
        string paymentMethod,
        string transactionId,
        decimal amount,
        string gatewayNote,
        string responseCodeMessage,
        CancellationToken cancellationToken)
    {
        var invoice = await _unitOfWork.Repository<Invoice>()
            .Find(i => i.InvoiceNumber == invoiceNumber)
            .SingleOrDefaultAsync(cancellationToken);

        if (invoice == null)
        {
            return new PaymentResultResponse
            {
                Success = false,
                Message = "Không tìm thấy hóa đơn tương ứng với giao dịch."
            };
        }

        var invoiceItem = await _unitOfWork.Repository<InvoiceItem>()
            .Find(item => item.InvoiceId == invoice.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (isSuccess)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;

            invoice.Status = "Paid";
            invoice.PaidAt = now;
            _unitOfWork.Repository<Invoice>().Update(invoice);

            if (invoiceItem?.SubscriptionId != null)
            {
                var subscription = await _unitOfWork.Repository<MemberSubscription>()
                    .GetByIdAsync(invoiceItem.SubscriptionId.Value, cancellationToken);
                if (subscription != null)
                {
                    subscription.Status = "Active";
                    subscription.UpdatedAt = now;
                    _unitOfWork.Repository<MemberSubscription>().Update(subscription);
                }
            }

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                MemberId = invoice.MemberId,
                PaymentMethod = paymentMethod,
                TransactionCode = transactionId,
                Amount = amount > 0 ? amount : invoice.TotalAmount,
                PaymentStatus = "Completed",
                PaidAt = now,
                Note = gatewayNote
            };
            await _unitOfWork.Repository<Payment>().AddAsync(payment, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PaymentResultResponse
            {
                Success = true,
                Message = "Thanh toán gói tập thành công!",
                InvoiceNumber = invoiceNumber,
                TransactionId = transactionId,
                Amount = amount > 0 ? amount : invoice.TotalAmount
            };
        }
        else
        {
            invoice.Status = "Cancelled";
            _unitOfWork.Repository<Invoice>().Update(invoice);

            if (invoiceItem?.SubscriptionId != null)
            {
                var subscription = await _unitOfWork.Repository<MemberSubscription>()
                    .GetByIdAsync(invoiceItem.SubscriptionId.Value, cancellationToken);
                if (subscription != null)
                {
                    subscription.Status = "Cancelled";
                    _unitOfWork.Repository<MemberSubscription>().Update(subscription);
                }
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new PaymentResultResponse
            {
                Success = false,
                Message = $"Thanh toán không thành công. {responseCodeMessage}",
                InvoiceNumber = invoiceNumber,
                Amount = amount > 0 ? amount : invoice.TotalAmount
            };
        }
    }

    #endregion
}

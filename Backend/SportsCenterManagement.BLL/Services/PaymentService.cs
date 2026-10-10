using System.Data;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.CoreFlows;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class PaymentService(
    IUnitOfWork unitOfWork,
    IVnPayService vnPayService,
    IMoMoService moMoService,
    IPayOsService payOsService,
    IConfiguration configuration) : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IVnPayService _vnPayService = vnPayService;
    private readonly IMoMoService _moMoService = moMoService;
    private readonly IPayOsService _payOsService = payOsService;
    private readonly IConfiguration _configuration = configuration;

    public async Task<string> CreatePaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string ipAddress,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureGatewayConfiguration("VNPay", "VnPay:TmnCode", "VnPay:HashSecret", "VnPay:BaseUrl", "VnPay:ReturnUrl");
        var (invoice, payment, existingLink) = await PrepareOnlinePaymentAsync(
            memberId, request, "VNPAY", idempotencyKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingLink))
        {
            return existingLink;
        }

        try
        {
            var url = _vnPayService.CreatePaymentUrl(
                payment.GatewayReference!,
                payment.Amount,
                $"Thanh toan hoa don {invoice.InvoiceNumber}",
                ipAddress,
                request.BankCode);
            await SavePaymentLinkAsync(payment.Id, url, cancellationToken);
            return url;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception)
        {
            await MarkPaymentPendingForReconciliationAsync(payment.Id, "Không tạo được URL VNPay; cần kiểm tra trước khi thử lại.");
            throw new BusinessException(HttpStatusCode.ServiceUnavailable,
                "Không thể tạo link thanh toán. Giao dịch được giữ Pending để tránh tạo khoản thu trùng.");
        }
    }

    public async Task<string> CreateMomoPaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureGatewayConfiguration("MoMo", "Momo:PartnerCode", "Momo:AccessKey", "Momo:SecretKey", "Momo:PaymentUrl", "Momo:ReturnUrl", "Momo:NotifyUrl");
        var (invoice, payment, existingLink) = await PrepareOnlinePaymentAsync(
            memberId, request, "MOMO", idempotencyKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingLink))
        {
            return existingLink;
        }

        try
        {
            var response = await _moMoService.CreatePaymentUrlAsync(
                payment.GatewayReference!,
                payment.Amount,
                $"Thanh toan hoa don {invoice.InvoiceNumber}",
                cancellationToken);

            var expectedPartnerCode = _configuration["Momo:PartnerCode"];
            var validProviderResponse = response.ResultCode == 0 &&
                                        response.OrderId == payment.GatewayReference &&
                                        response.RequestId == payment.GatewayReference &&
                                        response.Amount == payment.Amount &&
                                        response.PartnerCode == expectedPartnerCode &&
                                        Uri.TryCreate(response.PayUrl, UriKind.Absolute, out var payUri) &&
                                        payUri.Scheme == Uri.UriSchemeHttps &&
                                        (payUri.Host.Equals("momo.vn", StringComparison.OrdinalIgnoreCase) ||
                                         payUri.Host.EndsWith(".momo.vn", StringComparison.OrdinalIgnoreCase));
            if (!validProviderResponse)
            {
                await SetPaymentStatusAsync(payment.Id, "Failed",
                    $"MoMo link creation could not be verified (result code {response.ResultCode}).", cancellationToken);
                throw BusinessException.Conflict("MoMo từ chối hoặc trả phản hồi tạo link không hợp lệ. Hóa đơn chưa thu tiền; hãy thử lại bằng idempotency key mới.");
            }

            await SavePaymentLinkAsync(payment.Id, response.PayUrl, cancellationToken);
            return response.PayUrl;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception)
        {
            await MarkPaymentPendingForReconciliationAsync(payment.Id, "MoMo chưa xác nhận kết quả khởi tạo; cần đối soát trước khi thử lại.");
            throw new BusinessException(HttpStatusCode.ServiceUnavailable,
                "Chưa xác định được kết quả tạo giao dịch MoMo. Hệ thống giữ Pending; quản lý cần đối soát trước lần thử mới.");
        }
    }

    public async Task<string> CreatePayOsPaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureGatewayConfiguration("PayOS", "PayOS:ClientId", "PayOS:ApiKey", "PayOS:ChecksumKey", "PayOS:BaseUrl", "PayOS:ReturnUrl", "PayOS:CancelUrl");
        EnsurePayOsUrlConfiguration();
        var (invoice, payment, existingLink) = await PrepareOnlinePaymentAsync(
            memberId, request, "PAYOS", idempotencyKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingLink))
        {
            return existingLink;
        }

        try
        {
            var response = await _payOsService.CreatePaymentLinkAsync(
                payment.Id,
                payment.Amount,
                $"HD{payment.Id.ToString(CultureInfo.InvariantCulture)}",
                "Sports Center membership",
                cancellationToken);
            var paymentData = response.Data;
            if (paymentData is null || paymentData.OrderCode != payment.Id ||
                paymentData.Amount != payment.Amount || string.IsNullOrWhiteSpace(paymentData.CheckoutUrl))
            {
                throw new InvalidOperationException("PayOS payment response did not match the stored payment attempt.");
            }

            await SavePaymentLinkAsync(payment.Id, paymentData.CheckoutUrl, cancellationToken);
            return paymentData.CheckoutUrl;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception)
        {
            await MarkPaymentPendingForReconciliationAsync(payment.Id, "PayOS chưa xác nhận kết quả khởi tạo; cần đối soát trước khi thử lại.");
            throw new BusinessException(HttpStatusCode.ServiceUnavailable,
                "Chưa xác định được kết quả tạo giao dịch PayOS. Hệ thống giữ Pending; quản lý cần đối soát trước lần thử mới.");
        }
    }

    public async Task<PaymentResultResponse> ProcessPaymentCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_configuration["VnPay:HashSecret"]))
        {
            return FailedCallback("VNPay chưa được cấu hình chữ ký.", providerAckCode: "97");
        }

        var callback = _vnPayService.ProcessCallback(queryParams);
        if (!callback.IsValidSignature)
        {
            return FailedCallback("Chữ ký VNPay không hợp lệ.", providerAckCode: "97");
        }
        if (!string.IsNullOrWhiteSpace(callback.CurrencyCode) &&
            !string.Equals(callback.CurrencyCode, "VND", StringComparison.OrdinalIgnoreCase))
        {
            if (callback.IsSuccess)
            {
                return await MarkSignedGatewayCallbackReviewRequiredAsync(
                    "VNPAY", callback.InvoiceNumber, "Currency callback không phải VND",
                    callback.Amount, callback.TransactionNo, cancellationToken);
            }
            return FailedCallback("Currency callback VNPay không hợp lệ.", providerAckCode: "04");
        }

        string note = callback.ResponseCode switch
        {
            "00" => "VNPay giao dịch thành công",
            "24" => "Giao dịch đã bị hủy bởi người dùng (ResponseCode 24)",
            "09" => "Giao dịch VNPay đang chờ xử lý (ResponseCode 09)",
            "11" => "Giao dịch không thành công: Đã hết hạn chờ thanh toán (ResponseCode 11)",
            "51" => "Tài khoản của quý khách không đủ số dư để thực hiện giao dịch (ResponseCode 51)",
            "79" => "Khách hàng nhập sai mật khẩu quá số lần quy định (ResponseCode 79)",
            _ => $"VNPay phản hồi mã {callback.ResponseCode}"
        };

        return await CompleteGatewayPaymentAsync(
            "VNPAY", callback.InvoiceNumber, null, callback.IsSuccess, callback.IsPending,
            callback.TransactionNo, callback.Amount, note, cancellationToken);
    }

    public Task<PaymentResultResponse> ProcessMomoCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
        var callback = _moMoService.ProcessCallback(queryParams);
        if (!callback.IsValidSignature)
        {
            return Task.FromResult(FailedCallback("Chữ ký MoMo không hợp lệ."));
        }

        return CompleteGatewayPaymentAsync(
            "MOMO", callback.OrderId, callback.RequestId, callback.IsSuccess, callback.IsPending,
            callback.TransId, callback.Amount, $"MoMo result {callback.ResultCode}", cancellationToken);
    }

    public async Task<PaymentResultResponse> ProcessPayOsWebhookAsync(
        PayOsWebhookRequest webhookRequest,
        CancellationToken cancellationToken = default)
    {
        if (!_payOsService.VerifyWebhookSignature(webhookRequest))
        {
            throw BusinessException.BadRequest("Chữ ký webhook PayOS không hợp lệ.");
        }

        var data = webhookRequest.Data!;
        if (!webhookRequest.Success || !string.Equals(webhookRequest.Code, "00", StringComparison.Ordinal) ||
            !string.Equals(data.Code, "00", StringComparison.Ordinal))
        {
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = "Webhook PayOS hợp lệ nhưng giao dịch chưa được xác nhận thành công. Payment vẫn Pending để đối soát.",
                Amount = data.Amount
            };
        }

        if (data.OrderCode <= 0 || data.Amount <= 0 ||
            !string.Equals(data.Currency, "VND", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(data.Reference))
        {
            if (data.OrderCode > 0)
            {
                return await MarkSignedGatewayCallbackReviewRequiredAsync(
                    "PAYOS", data.OrderCode.ToString(CultureInfo.InvariantCulture),
                    !string.Equals(data.Currency, "VND", StringComparison.Ordinal)
                        ? "Currency callback không phải VND"
                        : "Webhook thiếu amount hoặc reference hợp lệ",
                    data.Amount, data.Reference, cancellationToken);
            }
            return FailedCallback("Webhook PayOS thiếu mã giao dịch, số tiền hoặc currency hợp lệ.");
        }

        return await CompleteGatewayPaymentAsync(
            "PAYOS",
            data.OrderCode.ToString(CultureInfo.InvariantCulture),
            null,
            true,
            false,
            data.Reference,
            data.Amount,
            $"PayOS payment {data.Desc}",
            cancellationToken);
    }

    public async Task<PaymentResultResponse> ReconcilePendingPaymentAsync(
        long managerUserId,
        long? managerCenterId,
        string gatewayReference,
        ReconcilePendingPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var evidence = request.EvidenceNote?.Trim() ?? string.Empty;
        if (evidence.Length < 10)
        {
            throw BusinessException.BadRequest("Bằng chứng đối soát phải có ít nhất 10 ký tự không tính khoảng trắng.");
        }

        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var payment = await _unitOfWork.Context.Payments
            .SingleOrDefaultAsync(candidate => candidate.GatewayReference == gatewayReference, cancellationToken)
            ?? throw BusinessException.NotFound("Không tìm thấy lần thử thanh toán.");
        var invoice = await _unitOfWork.Context.Invoices
            .SingleAsync(candidate => candidate.Id == payment.InvoiceId, cancellationToken);
        EnsureCenterScope(managerCenterId, invoice.CenterId);

        if (payment.PaymentStatus == "Succeeded")
        {
            await transaction.CommitAsync(cancellationToken);
            return PaymentSuccess(invoice, payment, "Giao dịch đã được xác nhận trước đó.");
        }
        if (payment.PaymentStatus is not ("Pending" or "Failed" or "Voided" or "ReviewRequired"))
        {
            throw BusinessException.Conflict("Trạng thái giao dịch không cho phép đối soát.");
        }

        var now = DateTime.UtcNow;
        if (request.Status == "Failed")
        {
            payment.PaymentStatus = "Failed";
            payment.Note = "Manager reconciliation: " + evidence;
            payment.PaidAt = null;
            _unitOfWork.Context.AuditLogs.Add(CreateAudit(managerUserId, "Payment.ReconciledFailed",
                "Payment", payment.Id, new { payment.GatewayReference, evidence }, now));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Message = "Đã ghi nhận giao dịch thất bại sau khi đối soát.",
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = payment.Amount
            };
        }

        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId))
        {
            throw BusinessException.BadRequest("Cần mã giao dịch từ cổng thanh toán để xác nhận thành công.");
        }
        if (payment.PaymentStatus == "ReviewRequired" && !request.ConfirmedAmount.HasValue)
        {
            throw BusinessException.BadRequest("Payment ReviewRequired cần ConfirmedAmount đúng theo bằng chứng đối soát.");
        }
        if (request.ConfirmedAmount.HasValue)
        {
            EnsurePaymentAmount(request.ConfirmedAmount.Value, "ConfirmedAmount");
            if (request.ConfirmedAmount.Value <= 0)
                throw BusinessException.BadRequest("ConfirmedAmount phải lớn hơn 0.");
            payment.Amount = request.ConfirmedAmount.Value;
            payment.AmountReceived = request.ConfirmedAmount.Value;
        }
        var normalizedId = NormalizeProviderTransactionId(GetProvider(payment.PaymentMethod), request.ProviderTransactionId);
        if (await _unitOfWork.Context.Payments.AnyAsync(
                candidate => candidate.ProviderTransactionId == normalizedId && candidate.Id != payment.Id,
                cancellationToken))
        {
            throw BusinessException.Conflict("Mã giao dịch của nhà cung cấp đã được ghi nhận cho payment khác.");
        }

        payment.PaymentStatus = "Succeeded";
        payment.ProviderTransactionId = normalizedId;
        payment.TransactionCode = normalizedId;
        payment.PaidAt = now;
        payment.Note = "Manager reconciliation: " + evidence;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await ApplyPaymentToInvoiceAsync(invoice, now, cancellationToken);
        _unitOfWork.Context.AuditLogs.Add(CreateAudit(managerUserId, "Payment.ReconciledSucceeded",
            "Payment", payment.Id, new { payment.GatewayReference, payment.Amount, request.ProviderTransactionId, evidence }, now));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PaymentSuccess(invoice, payment, "Đã đối soát và ghi nhận giao dịch thành công.");
    }

    public async Task<CounterPaymentResponse> ProcessCounterPaymentAsync(
        long staffUserId,
        long? staffCenterId,
        string idempotencyKey,
        CounterPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var scopedKey = ScopeIdempotencyKey(staffUserId, idempotencyKey);
        var method = request.PaymentMethod.Trim().ToUpperInvariant();
        if (method is not ("CASH" or "POS"))
        {
            throw BusinessException.BadRequest("Tại quầy chỉ nhận CASH hoặc POS. Thanh toán trực tuyến phải dùng link cổng thanh toán.");
        }

        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await _unitOfWork.Context.Invoices
            .SingleOrDefaultAsync(invoice => invoice.IdempotencyKey == scopedKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.MemberId != request.MemberId ||
                !await InvoiceHasPackageAsync(existing.Id, request.PackageId, cancellationToken))
            {
                throw BusinessException.Conflict("Idempotency-Key đã được dùng cho một giao dịch khác.");
            }
            var existingPayment = await _unitOfWork.Context.Payments
                .SingleOrDefaultAsync(payment => payment.IdempotencyKey == scopedKey, cancellationToken);
            if (existingPayment is null || existingPayment.PaymentMethod != method ||
                existingPayment.AmountReceived != (request.AmountReceived == 0 && method == "POS"
                    ? existingPayment.Amount
                    : request.AmountReceived) ||
                (method == "POS" && existingPayment.TransactionCode != $"POS:{request.PosApprovalCode?.Trim()}"))
            {
                throw BusinessException.Conflict("Idempotency-Key đã được dùng với dữ liệu thanh toán khác.");
            }
            var duplicateResponse = await BuildCounterResponseAsync(existing, existingPayment, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return duplicateResponse;
        }

        var member = await _unitOfWork.Context.MemberProfiles
            .SingleOrDefaultAsync(candidate => candidate.Id == request.MemberId, cancellationToken)
            ?? throw BusinessException.NotFound("Không tìm thấy hội viên.");
        if (!await _unitOfWork.Context.Users.AnyAsync(
                user => user.Id == member.UserId && user.Status == "Active"
                    && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow), cancellationToken))
        {
            throw BusinessException.Conflict("Tài khoản hội viên không hoạt động.");
        }

        var package = await _unitOfWork.Context.MembershipPackages
            .SingleOrDefaultAsync(candidate => candidate.Id == request.PackageId && candidate.Status == "Active", cancellationToken)
            ?? throw BusinessException.NotFound("Gói tập không tồn tại hoặc đã ngừng hoạt động.");
        EnsureCenterScope(staffCenterId, package.CenterId);
        if (member.CenterId.HasValue && member.CenterId.Value != package.CenterId)
        {
            throw BusinessException.Conflict("Hội viên và gói tập không thuộc cùng trung tâm.");
        }
        if (!await _unitOfWork.Context.Centers.AnyAsync(
                center => center.Id == package.CenterId && center.Status == "Active", cancellationToken))
        {
            throw BusinessException.Conflict("Trung tâm của gói tập không còn hoạt động.");
        }
        if (package.Price <= 0 || package.DurationDays <= 0)
        {
            throw BusinessException.Conflict("Gói tập phải có giá và thời hạn hợp lệ trước khi thu tiền.");
        }
        var pendingInvoiceNumber = await FindOpenPendingInvoiceNumberAsync(member.Id, package.Id, cancellationToken);
        if (pendingInvoiceNumber is not null)
        {
            if (request.CancelPendingIfAny)
            {
                var existingPendingInv = await _unitOfWork.Context.Invoices
                    .SingleOrDefaultAsync(i => i.InvoiceNumber == pendingInvoiceNumber, cancellationToken);
                if (existingPendingInv is not null)
                {
                    await CancelPendingInvoiceCoreAsync(staffUserId, existingPendingInv, DateTime.UtcNow, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            else
            {
                throw BusinessException.Conflict(
                    $"Hội viên đã có hóa đơn chờ thanh toán cho gói này ({pendingInvoiceNumber}); hãy tiếp tục thanh toán hóa đơn đó hoặc hủy đơn chờ để thanh toán mới.");
            }
        }

        var received = request.AmountReceived;
        if (method == "POS" && received == 0)
        {
            received = package.Price;
        }
        EnsurePaymentAmount(received, "Số tiền nhận");
        if (received <= 0)
        {
            throw BusinessException.BadRequest("Số tiền nhận phải lớn hơn 0.");
        }
        if (method == "POS" &&
            (string.IsNullOrWhiteSpace(request.PosApprovalCode) || received != package.Price))
        {
            throw BusinessException.BadRequest("POS cần mã chuẩn chi và phải thu đúng tổng hóa đơn.");
        }
        if (method == "POS" && await _unitOfWork.Context.Payments.AnyAsync(
                payment => payment.TransactionCode == $"POS:{request.PosApprovalCode!.Trim()}", cancellationToken))
        {
            throw BusinessException.Conflict("Mã chuẩn chi POS đã được ghi nhận trước đó.");
        }

        var captured = Math.Min(received, package.Price);
        var now = DateTime.UtcNow;
        if (!member.CenterId.HasValue)
        {
            member.CenterId = package.CenterId;
            member.UpdatedAt = now;
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
        _unitOfWork.Context.MemberSubscriptions.Add(subscription);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var invoice = new Invoice
        {
            InvoiceNumber = CreateInvoiceNumber(now),
            IdempotencyKey = scopedKey,
            MemberId = member.Id,
            CenterId = package.CenterId,
            CreatedBy = staffUserId,
            Subtotal = package.Price,
            Discount = 0,
            Tax = 0,
            TotalAmount = package.Price,
            Status = captured == package.Price ? "Paid" : "PartiallyPaid",
            IssuedAt = now,
            PaidAt = captured == package.Price ? now : null
        };
        _unitOfWork.Context.Invoices.Add(invoice);
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        _unitOfWork.Context.InvoiceItems.Add(new InvoiceItem
        {
            InvoiceId = invoice.Id,
            PackageId = package.Id,
            SubscriptionId = subscription.Id,
            Description = $"Gói tập: {package.Name}",
            Quantity = 1,
            UnitPrice = package.Price,
            Amount = package.Price
        });
        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = member.Id,
            ProcessedBy = staffUserId,
            PaymentMethod = method,
            TransactionCode = method == "POS" ? $"POS:{request.PosApprovalCode!.Trim()}" : $"CASH:{Guid.NewGuid():N}",
            Amount = captured,
            AmountReceived = received,
            PaymentStatus = "Succeeded",
            CreatedAt = now,
            AttemptedAt = now,
            PaidAt = now,
            IdempotencyKey = scopedKey,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };
        _unitOfWork.Context.Payments.Add(payment);
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);

        if (invoice.Status == "Paid")
        {
            await ActivateInvoiceSubscriptionsAsync(invoice.Id, now, cancellationToken);
        }
        _unitOfWork.Context.AuditLogs.Add(CreateAudit(staffUserId, "Payment.CounterRecorded", "Invoice", invoice.Id,
            new { invoice.InvoiceNumber, payment.PaymentMethod, payment.Amount, invoice.Status }, now));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildCounterResponseAsync(invoice, payment, cancellationToken);
    }

    public async Task<PaymentResultResponse> VoidCounterPaymentAsync(
        long staffUserId,
        long? staffCenterId,
        string invoiceNumber,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
        {
            throw BusinessException.BadRequest("Lý do void phải có ít nhất 5 ký tự.");
        }

        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await _unitOfWork.Context.Invoices
            .SingleOrDefaultAsync(candidate => candidate.InvoiceNumber == invoiceNumber, cancellationToken)
            ?? throw BusinessException.NotFound("Không tìm thấy hóa đơn.");
        EnsureCenterScope(staffCenterId, invoice.CenterId);
        if (await _unitOfWork.Context.Payments.AnyAsync(
                payment => payment.InvoiceId == invoice.Id && payment.PaymentStatus == "Succeeded", cancellationToken))
        {
            throw BusinessException.Conflict("Hóa đơn đã nhận tiền. Dùng refund để hoàn tiền; không void giao dịch đã thu.");
        }
        if (invoice.Status is "Voided" or "Refunded" or "Paid")
        {
            throw BusinessException.Conflict($"Hóa đơn trạng thái {invoice.Status} không thể void.");
        }

        var now = DateTime.UtcNow;
        invoice.Status = "Voided";
        foreach (var payment in await _unitOfWork.Context.Payments
                     .Where(candidate => candidate.InvoiceId == invoice.Id && candidate.PaymentStatus == "Pending")
                     .ToListAsync(cancellationToken))
        {
            payment.PaymentStatus = "Voided";
            payment.Note = "Invoice voided: " + reason.Trim();
        }
        var invoiceItems = await _unitOfWork.Context.InvoiceItems
            .Where(item => item.InvoiceId == invoice.Id && item.SubscriptionId != null)
            .ToListAsync(cancellationToken);
        foreach (var item in invoiceItems)
        {
            var subscription = await _unitOfWork.Context.MemberSubscriptions
                .SingleOrDefaultAsync(candidate => candidate.Id == item.SubscriptionId, cancellationToken);
            if (subscription is not null && subscription.Status == "PendingPayment")
            {
                subscription.Status = "Cancelled";
                subscription.UpdatedAt = now;
            }
        }
        _unitOfWork.Context.AuditLogs.Add(CreateAudit(staffUserId, "Invoice.Voided", "Invoice", invoice.Id,
            new { invoice.InvoiceNumber, reason = reason.Trim() }, now));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PaymentResultResponse { Success = true, Message = "Đã void hóa đơn chưa thu tiền.", InvoiceNumber = invoice.InvoiceNumber };
    }

    public async Task<InvoiceDetailsResponse?> GetInvoiceAsync(
        long actorUserId,
        string actorRole,
        long? actorCenterId,
        string invoiceNumber,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _unitOfWork.Context.Invoices
            .SingleOrDefaultAsync(candidate => candidate.InvoiceNumber == invoiceNumber, cancellationToken);
        if (invoice is null)
        {
            return null;
        }
        var isAdmin = string.Equals(actorRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var isStaffInCenter = actorCenterId == invoice.CenterId &&
                              (actorRole is "Manager" or "Receptionist");
        var ownsInvoice = await _unitOfWork.Context.MemberProfiles
            .AnyAsync(profile => profile.Id == invoice.MemberId && profile.UserId == actorUserId, cancellationToken);
        if (!isAdmin && !isStaffInCenter && !ownsInvoice)
        {
            return null;
        }
        return await BuildInvoiceDetailsAsync(invoice, cancellationToken);
    }

    public async Task<InvoiceDetailsResponse> RecordInvoicePaymentAsync(
        long staffUserId,
        long? staffCenterId,
        string invoiceNumber,
        string idempotencyKey,
        RecordInvoicePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePaymentAmount(request.Amount, "Số tiền thanh toán");
        var scopedKey = ScopeIdempotencyKey(staffUserId, idempotencyKey);
        var method = request.PaymentMethod.Trim().ToUpperInvariant();
        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await _unitOfWork.Context.Invoices
            .SingleOrDefaultAsync(candidate => candidate.InvoiceNumber == invoiceNumber, cancellationToken)
            ?? throw BusinessException.NotFound("Không tìm thấy hóa đơn.");
        EnsureCenterScope(staffCenterId, invoice.CenterId);

        var existingPayment = await _unitOfWork.Context.Payments
            .SingleOrDefaultAsync(candidate => candidate.IdempotencyKey == scopedKey, cancellationToken);
        if (existingPayment is not null)
        {
            if (existingPayment.InvoiceId != invoice.Id)
            {
                throw BusinessException.Conflict("Idempotency-Key đã được dùng cho hóa đơn khác.");
            }
            if (existingPayment.Amount != request.Amount || existingPayment.PaymentMethod != method ||
                (method == "POS" && existingPayment.TransactionCode != $"POS:{request.PosApprovalCode?.Trim()}"))
            {
                throw BusinessException.Conflict("Idempotency-Key đã được dùng với dữ liệu thanh toán khác.");
            }
            await transaction.CommitAsync(cancellationToken);
            return await BuildInvoiceDetailsAsync(invoice, cancellationToken);
        }
        if (invoice.Status is not ("Issued" or "PartiallyPaid"))
        {
            throw BusinessException.Conflict("Hóa đơn không còn số dư cần thanh toán.");
        }
        if (!await (
                from profile in _unitOfWork.Context.MemberProfiles
                join user in _unitOfWork.Context.Users on profile.UserId equals user.Id
                where profile.Id == invoice.MemberId && user.Status == "Active"
                    && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)
                select profile.Id).AnyAsync(cancellationToken))
        {
            throw BusinessException.Conflict("Tài khoản hội viên không hoạt động hoặc đang bị khóa.");
        }

        var alreadyPaid = await GetSucceededAmountAsync(invoice.Id, cancellationToken);
        var remaining = invoice.TotalAmount - alreadyPaid;
        if (request.Amount <= 0 || request.Amount > remaining)
        {
            throw BusinessException.Conflict("Số tiền thanh toán vượt số dư hóa đơn.");
        }
        if (method == "POS")
        {
            if (string.IsNullOrWhiteSpace(request.PosApprovalCode) || request.Amount != remaining)
            {
                throw BusinessException.BadRequest("POS cần mã chuẩn chi và phải thu đúng số dư còn lại.");
            }
            if (await _unitOfWork.Context.Payments.AnyAsync(
                    payment => payment.TransactionCode == $"POS:{request.PosApprovalCode!.Trim()}", cancellationToken))
            {
                throw BusinessException.Conflict("Mã chuẩn chi POS đã được ghi nhận trước đó.");
            }
        }
        else if (method != "CASH")
        {
            throw BusinessException.BadRequest("Phương thức nội bộ chỉ nhận CASH hoặc POS.");
        }

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = invoice.MemberId,
            ProcessedBy = staffUserId,
            PaymentMethod = method,
            TransactionCode = method == "POS" ? $"POS:{request.PosApprovalCode!.Trim()}" : $"CASH:{Guid.NewGuid():N}",
            Amount = request.Amount,
            AmountReceived = request.Amount,
            PaymentStatus = "Succeeded",
            CreatedAt = now,
            AttemptedAt = now,
            PaidAt = now,
            IdempotencyKey = scopedKey,
            Note = request.Note?.Trim()
        };
        _unitOfWork.Context.Payments.Add(payment);
        var totalPaid = alreadyPaid + payment.Amount;
        invoice.Status = totalPaid == invoice.TotalAmount ? "Paid" : "PartiallyPaid";
        invoice.PaidAt = invoice.Status == "Paid" ? now : null;
        if (invoice.Status == "Paid")
        {
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            await ActivateInvoiceSubscriptionsAsync(invoice.Id, now, cancellationToken);
        }
        _unitOfWork.Context.AuditLogs.Add(CreateAudit(staffUserId, "Payment.InvoiceRecorded", "Invoice", invoice.Id,
            new { invoice.InvoiceNumber, payment.PaymentMethod, payment.Amount, invoice.Status }, now));
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildInvoiceDetailsAsync(invoice, cancellationToken);
    }

    public async Task<PaymentRefundResponse> RefundPaymentAsync(
        long managerUserId,
        long? managerCenterId,
        long paymentId,
        string idempotencyKey,
        CreateRefundRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        EnsurePaymentAmount(request.Amount, "Số tiền hoàn");
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5)
        {
            throw BusinessException.BadRequest("Lý do refund phải có ít nhất 5 ký tự không tính khoảng trắng.");
        }

        var scopedKey = ScopeIdempotencyKey(managerUserId, idempotencyKey);
        Payment payment;
        Invoice invoice;
        PaymentRefund refund;

        await using (var transaction = await _unitOfWork.Context.Database
                         .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var existing = await _unitOfWork.Context.PaymentRefunds
                .SingleOrDefaultAsync(candidate => candidate.IdempotencyKey == scopedKey, cancellationToken);
            if (existing is not null)
            {
                var oldPayment = await _unitOfWork.Context.Payments
                    .SingleAsync(candidate => candidate.Id == existing.PaymentId, cancellationToken);
                var oldInvoice = await _unitOfWork.Context.Invoices
                    .SingleAsync(candidate => candidate.Id == oldPayment.InvoiceId, cancellationToken);
                EnsureCenterScope(managerCenterId, oldInvoice.CenterId);
                if (existing.PaymentId != paymentId || existing.Amount != request.Amount ||
                    !string.Equals(existing.Reason, reason, StringComparison.Ordinal) ||
                    (oldPayment.PaymentMethod.Equals("POS", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(existing.ExternalReference, request.ExternalRefundReference?.Trim(), StringComparison.Ordinal)))
                {
                    throw BusinessException.Conflict("Idempotency-Key đã được dùng với dữ liệu refund khác.");
                }
                await transaction.CommitAsync(cancellationToken);
                return BuildRefundResponse(existing, oldPayment, oldInvoice, "Phản hồi idempotent của yêu cầu refund.");
            }

            payment = await _unitOfWork.Context.Payments
                .SingleOrDefaultAsync(candidate => candidate.Id == paymentId, cancellationToken)
                ?? throw BusinessException.NotFound("Không tìm thấy payment.");
            invoice = await _unitOfWork.Context.Invoices
                .SingleAsync(candidate => candidate.Id == payment.InvoiceId, cancellationToken);
            EnsureCenterScope(managerCenterId, invoice.CenterId);
            if (payment.PaymentStatus != "Succeeded")
            {
                throw BusinessException.Conflict("Chỉ giao dịch đã thu thành công mới được hoàn tiền.");
            }
            if (payment.PaymentMethod.Equals("POS", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(request.ExternalRefundReference))
            {
                throw BusinessException.BadRequest("Sau khi hoàn trên máy POS, cần gửi ExternalRefundReference.");
            }

            var reserved = await _unitOfWork.Context.PaymentRefunds
                .Where(candidate => candidate.PaymentId == payment.Id &&
                                    (candidate.Status == "Pending" || candidate.Status == "Succeeded"))
                .SumAsync(candidate => (decimal?)candidate.Amount, cancellationToken) ?? 0m;
            var refundable = payment.Amount - reserved;
            if (request.Amount <= 0 || request.Amount > refundable)
            {
                throw BusinessException.Conflict($"Số tiền tối đa có thể hoàn là {Math.Max(0m, refundable):N0} VND.");
            }
            if (payment.PaymentMethod.StartsWith("MOMO", StringComparison.OrdinalIgnoreCase) &&
                request.Amount != decimal.Truncate(request.Amount))
            {
                throw BusinessException.BadRequest("MoMo chỉ nhận số tiền refund VND nguyên.");
            }
            if (payment.PaymentMethod.StartsWith("MOMO", StringComparison.OrdinalIgnoreCase) &&
                (request.Amount < 1_000m || request.Amount > 50_000_000m ||
                 !long.TryParse(StripProviderPrefix(payment.ProviderTransactionId ?? string.Empty, "MOMO"),
                     NumberStyles.None, CultureInfo.InvariantCulture, out var momoTransactionId) || momoTransactionId <= 0))
            {
                throw BusinessException.Conflict("Số tiền hoặc mã giao dịch MoMo không đủ điều kiện refund.");
            }
            if (payment.PaymentMethod.StartsWith("VNPAY", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(payment.GatewayReference) ||
                 string.IsNullOrWhiteSpace(payment.ProviderTransactionId) || !payment.PaidAt.HasValue ||
                 !long.TryParse(StripProviderPrefix(payment.ProviderTransactionId ?? string.Empty, "VNPAY"),
                     NumberStyles.None, CultureInfo.InvariantCulture, out var vnPayTransactionId) || vnPayTransactionId <= 0))
            {
                throw BusinessException.Conflict("Payment thiếu mã giao dịch/ngày thanh toán để refund qua VNPay.");
            }
            if (!payment.PaymentMethod.Equals("CASH", StringComparison.OrdinalIgnoreCase) &&
                !payment.PaymentMethod.Equals("POS", StringComparison.OrdinalIgnoreCase) &&
                !payment.PaymentMethod.StartsWith("VNPAY", StringComparison.OrdinalIgnoreCase) &&
                !payment.PaymentMethod.StartsWith("MOMO", StringComparison.OrdinalIgnoreCase))
            {
                throw BusinessException.Conflict("Nhà cung cấp của giao dịch này chưa hỗ trợ quy trình refund.");
            }

            refund = new PaymentRefund
            {
                PaymentId = payment.Id,
                Amount = request.Amount,
                Status = "Pending",
                IdempotencyKey = scopedKey,
                ExternalReference = string.IsNullOrWhiteSpace(request.ExternalRefundReference)
                    ? null
                    : request.ExternalRefundReference.Trim(),
                Reason = reason,
                RequestedBy = managerUserId,
                CreatedAt = DateTime.UtcNow
            };
            _unitOfWork.Context.PaymentRefunds.Add(refund);
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            if (payment.PaymentMethod.Equals("CASH", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(refund.ExternalReference))
            {
                refund.ExternalReference = $"CASH-REF-{refund.Id}";
                await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        var method = payment.PaymentMethod.ToUpperInvariant();
        if (method == "CASH")
        {
            refund.Status = "Succeeded";
            refund.ProcessedAt = DateTime.UtcNow;
        }
        else if (method == "POS")
        {
            refund.Status = "Succeeded";
            refund.ProcessedAt = DateTime.UtcNow;
        }
        else
        {
            var requestId = $"RF{refund.Id:D20}";
            try
            {
                ProviderRefundResult providerResult;
                if (method.StartsWith("VNPAY", StringComparison.Ordinal))
                {
                    var paymentReference = payment.GatewayReference;
                    var providerTransactionId = payment.ProviderTransactionId;
                    var paidAt = payment.PaidAt;
                    if (string.IsNullOrWhiteSpace(paymentReference)
                        || string.IsNullOrWhiteSpace(providerTransactionId) || !paidAt.HasValue)
                    {
                        throw BusinessException.Conflict("Payment thiếu dữ liệu đối soát để refund qua VNPay.");
                    }
                    var refundedBefore = await GetSuccessfulRefundAmountAsync(payment.Id, cancellationToken);
                    var isFullRefund = refundedBefore == 0m && request.Amount == payment.Amount;
                    providerResult = await _vnPayService.RefundAsync(
                        paymentReference,
                        requestId,
                        StripProviderPrefix(providerTransactionId, "VNPAY"),
                        isFullRefund,
                        paidAt.Value,
                        request.Amount,
                        reason,
                        managerUserId.ToString(CultureInfo.InvariantCulture),
                        ipAddress,
                        cancellationToken);
                }
                else if (method.StartsWith("MOMO", StringComparison.Ordinal))
                {
                    var providerTransactionId = payment.ProviderTransactionId;
                    if (string.IsNullOrWhiteSpace(providerTransactionId))
                        throw BusinessException.Conflict("Payment thiếu mã giao dịch để refund qua MoMo.");
                    providerResult = await _moMoService.RefundAsync(
                        requestId,
                        $"RFO{refund.Id}",
                        StripProviderPrefix(providerTransactionId, "MOMO"),
                        request.Amount,
                        reason,
                        cancellationToken);
                }
                else
                {
                    throw BusinessException.Conflict("Nhà cung cấp của giao dịch này chưa hỗ trợ quy trình refund.");
                }

                if (providerResult.IsAuthenticated)
                {
                    refund.ProviderRefundId = NormalizeProviderRefundId(method, providerResult.ProviderRefundId);
                    refund.Status = providerResult.Status == "Succeeded" && refund.ProviderRefundId is null
                        ? "Pending"
                        : providerResult.Status;
                    refund.ProcessedAt = refund.Status == "Pending" ? null : DateTime.UtcNow;
                }
            }
            catch (BusinessException)
            {
                refund.Status = "Failed";
                refund.ProcessedAt = DateTime.UtcNow;
                _unitOfWork.Context.AuditLogs.Add(CreateAudit(managerUserId, "Payment.RefundFailed",
                    "PaymentRefund", refund.Id,
                    new { payment.Id, refund.Amount, reason = refund.Reason, outcome = "Provider request rejected before confirmation" },
                    DateTime.UtcNow));
                await _unitOfWork.Context.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception)
            {
                refund.Status = "Pending";
                _unitOfWork.Context.AuditLogs.Add(CreateAudit(managerUserId, "Payment.RefundPending",
                    "PaymentRefund", refund.Id,
                    new { payment.Id, refund.Amount, reason = refund.Reason, outcome = "Provider response was not confirmed" },
                    DateTime.UtcNow));
                await _unitOfWork.Context.SaveChangesAsync(CancellationToken.None);
                return BuildRefundResponse(refund, payment, invoice,
                    "Nhà cung cấp chưa xác nhận kết quả refund. Giao dịch vẫn Pending để đối soát.");
            }
        }

        var providerStatus = refund.Status;
        var providerRefundId = refund.ProviderRefundId;
        var providerProcessedAt = refund.ProcessedAt;
        await using (var transaction = await _unitOfWork.Context.Database
                         .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            await _unitOfWork.Context.Entry(refund).ReloadAsync(cancellationToken);
            if (refund.Status is "Succeeded" or "Failed")
            {
                await transaction.CommitAsync(cancellationToken);
                return BuildRefundResponse(refund, payment, invoice, "Refund đã được đối soát trong một yêu cầu khác.");
            }
            refund.Status = providerStatus;
            refund.ProviderRefundId = providerRefundId;
            refund.ProcessedAt = providerProcessedAt;
            if (refund.Status == "Succeeded")
            {
                payment.RefundApprovedBy = managerUserId;
                await UpdateInvoiceRefundStatusAsync(invoice, DateTime.UtcNow, cancellationToken);
            }
            _unitOfWork.Context.AuditLogs.Add(CreateAudit(managerUserId, "Payment.Refund" + refund.Status,
                "PaymentRefund", refund.Id,
                new { payment.Id, refund.Amount, refund.Status, refund.ProviderRefundId, reason = refund.Reason },
                DateTime.UtcNow));
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var message = refund.Status switch
        {
            "Succeeded" => "Refund hoàn tất.",
            "Failed" => "Nhà cung cấp từ chối refund.",
            _ => "Refund đang Pending; chưa trừ vào doanh thu cho đến khi được xác nhận."
        };
        return BuildRefundResponse(refund, payment, invoice, message);
    }

    public async Task<PaymentRefundResponse> ReconcileRefundAsync(
        long managerUserId,
        long? managerCenterId,
        long refundId,
        ReconcileRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var desiredStatus = request.Status?.Trim() ?? string.Empty;
        var evidence = request.EvidenceNote?.Trim() ?? string.Empty;
        if (desiredStatus is not ("Succeeded" or "Failed"))
        {
            throw BusinessException.BadRequest("Trạng thái đối soát refund chỉ nhận Succeeded hoặc Failed.");
        }
        if (evidence.Length < 10)
        {
            throw BusinessException.BadRequest("Bằng chứng đối soát phải có ít nhất 10 ký tự không tính khoảng trắng.");
        }

        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var refund = await _unitOfWork.Context.PaymentRefunds
            .SingleOrDefaultAsync(candidate => candidate.Id == refundId, cancellationToken)
            ?? throw BusinessException.NotFound("Không tìm thấy yêu cầu refund.");
        var payment = await _unitOfWork.Context.Payments
            .SingleAsync(candidate => candidate.Id == refund.PaymentId, cancellationToken);
        var invoice = await _unitOfWork.Context.Invoices
            .SingleAsync(candidate => candidate.Id == payment.InvoiceId, cancellationToken);
        EnsureCenterScope(managerCenterId, invoice.CenterId);
        var provider = GetProvider(payment.PaymentMethod);
        var isGatewayProvider = provider is "VNPAY" or "MOMO";
        var suppliedProviderRefundId = isGatewayProvider
            ? NormalizeProviderRefundId(provider, request.ProviderRefundId)
            : null;

        if (refund.Status != "Pending")
        {
            if (refund.Status == desiredStatus &&
                (desiredStatus == "Failed" || refund.ProviderRefundId == suppliedProviderRefundId))
            {
                await transaction.CommitAsync(cancellationToken);
                return BuildRefundResponse(refund, payment, invoice, "Refund đã được đối soát trước đó.");
            }
            throw BusinessException.Conflict("Yêu cầu refund đã chốt trạng thái khác và không thể ghi đè.");
        }
        if (desiredStatus == "Succeeded" && isGatewayProvider &&
            string.IsNullOrWhiteSpace(suppliedProviderRefundId))
        {
            throw BusinessException.BadRequest("Cần mã refund do cổng thanh toán trả về để xác nhận thành công.");
        }
        if (desiredStatus == "Succeeded" && !string.IsNullOrWhiteSpace(suppliedProviderRefundId) &&
            await _unitOfWork.Context.PaymentRefunds.AnyAsync(
                candidate => candidate.Id != refund.Id && candidate.ProviderRefundId == suppliedProviderRefundId,
                cancellationToken))
        {
            throw BusinessException.Conflict("Mã refund đã được ghi nhận cho yêu cầu khác.");
        }

        var now = DateTime.UtcNow;
        refund.Status = desiredStatus;
        refund.ProviderRefundId = desiredStatus == "Succeeded" ? suppliedProviderRefundId : null;
        refund.ProcessedAt = now;
        _unitOfWork.Context.AuditLogs.Add(CreateAudit(managerUserId,
            desiredStatus == "Succeeded" ? "Payment.RefundReconciledSucceeded" : "Payment.RefundReconciledFailed",
            "PaymentRefund", refund.Id,
            new { payment.Id, refund.Amount, refund.ProviderRefundId, evidence }, now));
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        if (desiredStatus == "Succeeded")
        {
            payment.RefundApprovedBy = managerUserId;
            await UpdateInvoiceRefundStatusAsync(invoice, now, cancellationToken);
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return BuildRefundResponse(refund, payment, invoice,
            desiredStatus == "Succeeded" ? "Đã đối soát refund thành công." : "Đã xác nhận refund thất bại; số tiền được mở lại để xử lý.");
    }

    public async Task<RevenueReportResponse> GetRevenueReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default)
    {
        if (from > to || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 366)
        {
            throw BusinessException.BadRequest("Khoảng ngày phải hợp lệ và không vượt quá 367 ngày.");
        }
        groupBy = (groupBy ?? string.Empty).Trim().ToLowerInvariant();
        if (groupBy is not ("day" or "month"))
        {
            throw BusinessException.BadRequest("groupBy chỉ nhận day hoặc month.");
        }
        if (!await _unitOfWork.Context.Centers.AnyAsync(center => center.Id == centerId, cancellationToken))
        {
            throw BusinessException.NotFound("Không tìm thấy trung tâm.");
        }

        var timeZone = GetReportTimeZone();
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);
        var payments = await (
            from payment in _unitOfWork.Context.Payments
            join invoice in _unitOfWork.Context.Invoices on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && payment.PaymentStatus == "Succeeded" &&
                  payment.PaidAt != null && payment.PaidAt >= startUtc && payment.PaidAt < endUtc
            select new { payment.Amount, PaidAt = payment.PaidAt!.Value })
            .ToListAsync(cancellationToken);
        var refunds = await (
            from refund in _unitOfWork.Context.PaymentRefunds
            join payment in _unitOfWork.Context.Payments on refund.PaymentId equals payment.Id
            join invoice in _unitOfWork.Context.Invoices on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && refund.Status == "Succeeded" &&
                  refund.ProcessedAt != null && refund.ProcessedAt >= startUtc && refund.ProcessedAt < endUtc
            select new { refund.Amount, ProcessedAt = refund.ProcessedAt!.Value })
            .ToListAsync(cancellationToken);

        DateOnly Period(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
            var day = DateOnly.FromDateTime(local);
            return groupBy == "month" ? new DateOnly(day.Year, day.Month, 1) : day;
        }

        var grossByPeriod = payments.GroupBy(row => Period(row.PaidAt))
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
        var refundsByPeriod = refunds.GroupBy(row => Period(row.ProcessedAt))
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
        var starts = new SortedSet<DateOnly>();
        for (var day = from; day <= to;)
        {
            var period = groupBy == "month" ? new DateOnly(day.Year, day.Month, 1) : day;
            starts.Add(period);
            day = groupBy == "month" ? period.AddMonths(1) : day.AddDays(1);
        }
        var periods = starts.Select(period =>
        {
            var gross = grossByPeriod.GetValueOrDefault(period);
            var refund = refundsByPeriod.GetValueOrDefault(period);
            return new RevenuePeriod(period, gross, refund, gross - refund);
        }).ToArray();
        var totalGross = payments.Sum(row => row.Amount);
        var totalRefunds = refunds.Sum(row => row.Amount);
        return new RevenueReportResponse(centerId, from, to, timeZone.Id, groupBy,
            totalGross, totalRefunds, totalGross - totalRefunds, periods);
    }

    private async Task<(Invoice Invoice, Payment Payment, string? ExistingLink)> PrepareOnlinePaymentAsync(
        long memberId,
        CreatePaymentRequest request,
        string provider,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber) && request.PackageId <= 0)
        {
            throw BusinessException.BadRequest("Cần PackageId để tạo hóa đơn mới.");
        }

        var scopedKey = ScopeIdempotencyKey(memberId, idempotencyKey);
        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var prior = await _unitOfWork.Context.Payments
            .SingleOrDefaultAsync(payment => payment.IdempotencyKey == scopedKey, cancellationToken);
        if (prior is not null)
        {
            var priorInvoice = await _unitOfWork.Context.Invoices
                .SingleAsync(invoice => invoice.Id == prior.InvoiceId, cancellationToken);
            if (!prior.PaymentMethod.StartsWith(provider, StringComparison.OrdinalIgnoreCase))
            {
                throw BusinessException.Conflict("Idempotency-Key đã được dùng cho phương thức khác.");
            }
            if ((request.PackageId > 0 && !await InvoiceHasPackageAsync(priorInvoice.Id, request.PackageId, cancellationToken)) ||
                (!string.IsNullOrWhiteSpace(request.InvoiceNumber) && priorInvoice.InvoiceNumber != request.InvoiceNumber))
            {
                throw BusinessException.Conflict("Idempotency-Key đã được dùng cho gói hoặc hóa đơn khác.");
            }
            if (prior.PaymentStatus != "Pending")
            {
                throw BusinessException.Conflict("Payment attempt đã kết thúc. Dùng key mới và InvoiceNumber để thử lại.");
            }
            if (string.IsNullOrWhiteSpace(prior.GatewayPaymentUrl))
            {
                throw BusinessException.Conflict("Payment attempt đang khởi tạo hoặc cần quản lý đối soát.");
            }
            await transaction.CommitAsync(cancellationToken);
            return (priorInvoice, prior, prior.GatewayPaymentUrl);
        }

        Invoice? invoice;
        if (!string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            invoice = await _unitOfWork.Context.Invoices.SingleOrDefaultAsync(
                candidate => candidate.InvoiceNumber == request.InvoiceNumber && candidate.MemberId == memberId,
                cancellationToken);
            if (invoice is null)
            {
                throw BusinessException.NotFound("Không tìm thấy hóa đơn của hội viên.");
            }
        }
        else
        {
            invoice = await _unitOfWork.Context.Invoices.SingleOrDefaultAsync(
                candidate => candidate.IdempotencyKey == scopedKey && candidate.MemberId == memberId,
                cancellationToken);
        }

        MembershipPackage package;
        if (invoice is null)
        {
            var member = await _unitOfWork.Context.MemberProfiles
                .SingleOrDefaultAsync(candidate => candidate.Id == memberId, cancellationToken)
                ?? throw BusinessException.NotFound("Không tìm thấy hồ sơ hội viên.");
            if (!await _unitOfWork.Context.Users.AnyAsync(
                    user => user.Id == member.UserId && user.Status == "Active"
                        && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow), cancellationToken))
            {
                throw BusinessException.Forbidden("Tài khoản hội viên không hoạt động.");
            }
            package = await _unitOfWork.Context.MembershipPackages
                .SingleOrDefaultAsync(candidate => candidate.Id == request.PackageId && candidate.Status == "Active", cancellationToken)
                ?? throw BusinessException.NotFound("Gói tập không tồn tại hoặc đã ngừng hoạt động.");
            if (package.Price <= 0 || package.DurationDays <= 0)
            {
                throw BusinessException.Conflict("Gói tập phải có giá và thời hạn lớn hơn 0.");
            }

            var now = DateTime.UtcNow;
            if (member.CenterId.HasValue && member.CenterId.Value != package.CenterId)
            {
                throw BusinessException.Forbidden("Gói tập không thuộc trung tâm của hội viên.");
            }
            if (!await _unitOfWork.Context.Centers.AnyAsync(
                    center => center.Id == package.CenterId && center.Status == "Active", cancellationToken))
            {
                throw BusinessException.Conflict("Trung tâm của gói tập không còn hoạt động.");
            }
            var pendingInvoiceNumber = await FindOpenPendingInvoiceNumberAsync(member.Id, package.Id, cancellationToken);
            if (pendingInvoiceNumber is not null)
            {
                if (request.CancelPendingIfAny)
                {
                    var existingPendingInv = await _unitOfWork.Context.Invoices
                        .SingleOrDefaultAsync(i => i.InvoiceNumber == pendingInvoiceNumber, cancellationToken);
                    if (existingPendingInv is not null)
                    {
                        await CancelPendingInvoiceCoreAsync(member.UserId, existingPendingInv, now, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                }
                else
                {
                    throw BusinessException.Conflict(
                        $"Hội viên đã có hóa đơn chờ thanh toán cho gói này ({pendingInvoiceNumber}); hãy tiếp tục thanh toán hóa đơn đó.");
                }
            }
            if (!member.CenterId.HasValue)
            {
                member.CenterId = package.CenterId;
                member.UpdatedAt = now;
            }
            var subscription = new MemberSubscription
            {
                MemberId = memberId,
                PackageId = package.Id,
                StartDate = null,
                EndDate = null,
                DurationDays = package.DurationDays,
                Price = package.Price,
                Status = "PendingPayment",
                AutoRenew = false,
                CreatedAt = now
            };
            _unitOfWork.Context.MemberSubscriptions.Add(subscription);
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            invoice = new Invoice
            {
                InvoiceNumber = CreateInvoiceNumber(now),
                IdempotencyKey = scopedKey,
                MemberId = memberId,
                CenterId = package.CenterId,
                CreatedBy = member.UserId,
                Subtotal = package.Price,
                Discount = 0,
                Tax = 0,
                TotalAmount = package.Price,
                Status = "Issued",
                IssuedAt = now
            };
            _unitOfWork.Context.Invoices.Add(invoice);
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            _unitOfWork.Context.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                PackageId = package.Id,
                SubscriptionId = subscription.Id,
                Description = package.Name,
                Quantity = 1,
                UnitPrice = package.Price,
                Amount = package.Price
            });
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (invoice.Status is not ("Issued" or "PartiallyPaid"))
            {
                throw BusinessException.Conflict("Hóa đơn không còn số dư để thanh toán.");
            }
            var item = await _unitOfWork.Context.InvoiceItems.SingleOrDefaultAsync(
                candidate => candidate.InvoiceId == invoice.Id && candidate.PackageId != null, cancellationToken)
                ?? throw BusinessException.Conflict("Hóa đơn không có dòng gói tập hợp lệ.");
            if (request.PackageId > 0 && item.PackageId != request.PackageId)
            {
                throw BusinessException.Conflict("PackageId không khớp hóa đơn.");
            }
            package = await _unitOfWork.Context.MembershipPackages
                .SingleAsync(candidate => candidate.Id == item.PackageId, cancellationToken);
        }

        var memberProfile = await _unitOfWork.Context.MemberProfiles
            .SingleAsync(candidate => candidate.Id == memberId, cancellationToken);
        if (!await _unitOfWork.Context.Users.AnyAsync(
                user => user.Id == memberProfile.UserId && user.Status == "Active"
                        && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow), cancellationToken))
        {
            throw BusinessException.Forbidden("Tài khoản hội viên không hoạt động.");
        }
        if (invoice.CenterId != package.CenterId ||
            (memberProfile.CenterId.HasValue && memberProfile.CenterId.Value != package.CenterId))
        {
            throw BusinessException.Forbidden("Hóa đơn, hội viên và gói tập không thuộc cùng trung tâm.");
        }
        if (!memberProfile.CenterId.HasValue)
        {
            memberProfile.CenterId = package.CenterId;
            memberProfile.UpdatedAt = DateTime.UtcNow;
        }

        var remaining = invoice.TotalAmount - await GetSucceededAmountAsync(invoice.Id, cancellationToken);
        if (remaining <= 0)
        {
            throw BusinessException.Conflict("Hóa đơn đã được thanh toán đủ.");
        }
        if ((provider is "MOMO" or "PAYOS") && remaining != decimal.Truncate(remaining))
        {
            throw BusinessException.Conflict($"{provider} chỉ nhận số tiền VND nguyên; số dư hóa đơn đang có phần thập phân.");
        }
        if (provider == "PAYOS" && remaining > int.MaxValue)
        {
            throw BusinessException.Conflict("PayOS không hỗ trợ số tiền vượt giới hạn số nguyên 32-bit.");
        }
        // Nếu hóa đơn có payment Pending trước đó (ví dụ mở cổng thanh toán nhưng chưa thanh toán/thử lại), void để khởi tạo lần mới
        var previousPendingPayments = await _unitOfWork.Context.Payments
            .Where(candidate => candidate.InvoiceId == invoice.Id && candidate.PaymentStatus == "Pending")
            .ToListAsync(cancellationToken);
        foreach (var p in previousPendingPayments)
        {
            p.PaymentStatus = "Voided";
            p.Note = "Giao dịch cũ được void để tạo liên kết thanh toán mới.";
        }

        var paymentAttemptTime = DateTime.UtcNow;
        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = invoice.MemberId,
            PaymentMethod = provider,
            Amount = remaining,
            PaymentStatus = "Pending",
            CreatedAt = paymentAttemptTime,
            AttemptedAt = paymentAttemptTime,
            IdempotencyKey = scopedKey,
            GatewayReference = $"PAY-{Guid.NewGuid():N}"
        };
        _unitOfWork.Context.Payments.Add(payment);
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        if (provider == "PAYOS")
        {
            payment.GatewayReference = payment.Id.ToString(CultureInfo.InvariantCulture);
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return (invoice, payment, null);
    }

    private async Task<PaymentResultResponse> CompleteGatewayPaymentAsync(
        string provider,
        string gatewayReference,
        string? requestId,
        bool isSuccess,
        bool isPending,
        string providerTransactionId,
        decimal amount,
        string note,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference))
        {
            return FailedCallback("Thiếu mã tham chiếu giao dịch.");
        }
        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var payment = await _unitOfWork.Context.Payments
            .SingleOrDefaultAsync(candidate => candidate.GatewayReference == gatewayReference, cancellationToken);
        if (payment is null || !payment.PaymentMethod.StartsWith(provider, StringComparison.OrdinalIgnoreCase))
        {
            return FailedCallback("Không tìm thấy payment attempt tương ứng.");
        }
        var invoice = await _unitOfWork.Context.Invoices
            .SingleAsync(candidate => candidate.Id == payment.InvoiceId, cancellationToken);
        if (payment.PaymentStatus == "ReviewRequired")
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = "Payment đang ReviewRequired; cần hoàn tất đối soát thủ công.",
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = payment.Amount
            };
        }
        if (provider == "MOMO" && !string.Equals(requestId, payment.GatewayReference, StringComparison.Ordinal))
        {
            if (isSuccess && payment.PaymentStatus != "Succeeded")
            {
                var review = await MarkPaymentReviewRequiredAsync(payment, provider,
                    "RequestId không khớp payment attempt", amount, providerTransactionId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return review;
            }
            return FailedCallback("RequestId không khớp payment attempt.", invoice.InvoiceNumber);
        }
        if (amount <= 0 || amount != payment.Amount)
        {
            if (isSuccess && payment.PaymentStatus != "Succeeded")
            {
                var review = await MarkPaymentReviewRequiredAsync(payment, provider,
                    "Amount không khớp payment attempt", amount, providerTransactionId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return review;
            }
            return FailedCallback("Amount không khớp payment attempt.", invoice.InvoiceNumber);
        }
        if (payment.PaymentStatus == "Succeeded")
        {
            await transaction.CommitAsync(cancellationToken);
            return payment.ProviderTransactionId == NormalizeProviderTransactionId(provider, providerTransactionId) &&
                   payment.Amount == amount
                ? PaymentSuccess(invoice, payment, "Callback đã được xử lý trước đó.")
                : FailedCallback("Payment attempt đã được chốt với dữ liệu khác.", invoice.InvoiceNumber);
        }
        if (payment.PaymentStatus == "Pending" && isPending && payment.Note == note)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = "Callback Pending đã được xử lý trước đó.",
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = amount
            };
        }
        if (payment.PaymentStatus == "Failed" && !isSuccess && !isPending && payment.Note == note)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = "Callback thất bại đã được xử lý trước đó.",
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = amount
            };
        }
        if (payment.PaymentStatus == "Voided" && !isSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = "Hóa đơn đã void; callback không xác nhận khoản thu mới.",
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = amount
            };
        }
        if (payment.PaymentStatus == "Refunded")
        {
            return FailedCallback("Payment attempt không còn nhận kết quả mới.", invoice.InvoiceNumber);
        }

        var now = DateTime.UtcNow;
        if (isPending)
        {
            payment.PaymentStatus = "Pending";
            payment.PaidAt = null;
            payment.Note = note;
            _unitOfWork.Context.AuditLogs.Add(CreateAudit(null, "Payment.GatewayPending", "Payment", payment.Id,
                new { provider, gatewayReference, note }, now));
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = "Cổng thanh toán đang xử lý. Hệ thống giữ giao dịch Pending và chưa cho phép thanh toán trùng.",
                InvoiceNumber = invoice.InvoiceNumber,
                TransactionId = providerTransactionId,
                Amount = amount
            };
        }
        if (!isSuccess)
        {
            payment.PaymentStatus = "Failed";
            payment.PaidAt = null;
            payment.Note = note;
            _unitOfWork.Context.AuditLogs.Add(CreateAudit(null, "Payment.GatewayFailed", "Payment", payment.Id,
                new { provider, gatewayReference, note }, now));
            await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = !string.IsNullOrWhiteSpace(note) ? note : "Cổng thanh toán báo thất bại; quyền lợi chưa được kích hoạt.",
                InvoiceNumber = invoice.InvoiceNumber,
                TransactionId = providerTransactionId,
                Amount = amount
            };
        }
        if (string.IsNullOrWhiteSpace(providerTransactionId))
        {
            if (isSuccess && payment.PaymentStatus != "Succeeded")
            {
                var review = await MarkPaymentReviewRequiredAsync(payment, provider,
                    "Provider không gửi transaction ID", amount, null, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return review;
            }
            return FailedCallback("Amount hoặc transaction id không khớp payment attempt.", invoice.InvoiceNumber);
        }
        var normalizedId = NormalizeProviderTransactionId(provider, providerTransactionId);
        if (await _unitOfWork.Context.Payments.AnyAsync(
                candidate => candidate.ProviderTransactionId == normalizedId && candidate.Id != payment.Id,
                cancellationToken))
        {
            if (isSuccess && payment.PaymentStatus != "Succeeded")
            {
                var review = await MarkPaymentReviewRequiredAsync(payment, provider,
                    "Provider transaction ID đã gắn với payment khác", amount, providerTransactionId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return review;
            }
            return FailedCallback("Mã giao dịch của cổng đã được ghi nhận.", invoice.InvoiceNumber);
        }

        var invoiceWasVoided = invoice.Status == "Voided";
        payment.PaymentStatus = "Succeeded";
        payment.ProviderTransactionId = normalizedId;
        payment.TransactionCode = normalizedId;
        payment.PaidAt = now;
        payment.Note = note;
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        await ApplyPaymentToInvoiceAsync(invoice, now, cancellationToken);
        _unitOfWork.Context.AuditLogs.Add(CreateAudit(null,
            invoiceWasVoided ? "Payment.GatewaySucceededAfterVoid" : "Payment.GatewaySucceeded",
            "Payment", payment.Id,
            new { provider, gatewayReference, providerTransactionId, payment.Amount, invoice.Status }, now));
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var message = invoice.Status == "PaidAfterVoid"
            ? "Cổng xác nhận đã thu tiền sau khi hóa đơn bị void; Manager cần hoàn lại khoản thu này."
            : invoice.Status == "Overpaid"
                ? "Thanh toán đã xác nhận nhưng hóa đơn bị thu thừa; cần hoàn lại phần dư."
                : "Thanh toán thành công.";
        return PaymentSuccess(invoice, payment, message);
    }

    private async Task ApplyPaymentToInvoiceAsync(Invoice invoice, DateTime paidAt, CancellationToken cancellationToken)
    {
        var totalPaid = await GetSucceededAmountAsync(invoice.Id, cancellationToken);
        if (invoice.Status is "Voided" or "Refunded")
        {
            invoice.Status = "PaidAfterVoid";
            invoice.PaidAt = paidAt;
            return;
        }
        if (totalPaid >= invoice.TotalAmount)
        {
            invoice.Status = totalPaid > invoice.TotalAmount ? "Overpaid" : "Paid";
            invoice.PaidAt = paidAt;
            await ActivateInvoiceSubscriptionsAsync(invoice.Id, paidAt, cancellationToken);
        }
        else
        {
            invoice.Status = "PartiallyPaid";
            invoice.PaidAt = null;
        }
    }

    private async Task ActivateInvoiceSubscriptionsAsync(long invoiceId, DateTime now, CancellationToken cancellationToken)
    {
        var subscriptions = await (
            from item in _unitOfWork.Context.InvoiceItems
            join subscription in _unitOfWork.Context.MemberSubscriptions on item.SubscriptionId equals subscription.Id
            where item.InvoiceId == invoiceId && item.SubscriptionId != null && subscription.Status == "PendingPayment"
            select subscription).ToListAsync(cancellationToken);
        foreach (var subscription in subscriptions)
        {
            var package = await _unitOfWork.Context.MembershipPackages
                .SingleAsync(candidate => candidate.Id == subscription.PackageId, cancellationToken);
            var today = ToBusinessDate(now);
            var currentEnd = await _unitOfWork.Context.MemberSubscriptions
                .Where(candidate => candidate.MemberId == subscription.MemberId &&
                                    candidate.Status == "Active" && candidate.EndDate >= today)
                .MaxAsync(candidate => (DateOnly?)candidate.EndDate, cancellationToken);
            subscription.StartDate = currentEnd.HasValue ? currentEnd.Value.AddDays(1) : today;
            var durationDays = subscription.DurationDays > 0 ? subscription.DurationDays : package.DurationDays;
            subscription.EndDate = subscription.StartDate.Value.AddDays(durationDays - 1);
            subscription.DurationDays = durationDays;
            subscription.Status = "Active";
            subscription.UpdatedAt = now;
        }
    }

    private async Task UpdateInvoiceRefundStatusAsync(Invoice invoice, DateTime now, CancellationToken cancellationToken)
    {
        var paid = await GetSucceededAmountAsync(invoice.Id, cancellationToken);
        var refunded = await (
            from refund in _unitOfWork.Context.PaymentRefunds
            join payment in _unitOfWork.Context.Payments on refund.PaymentId equals payment.Id
            where payment.InvoiceId == invoice.Id && refund.Status == "Succeeded"
            select (decimal?)refund.Amount).SumAsync(cancellationToken) ?? 0m;
        if (refunded <= 0)
        {
            return;
        }
        invoice.Status = refunded >= paid ? "Refunded" : "PartiallyRefunded";
        if (invoice.Status == "Refunded")
        {
            var subscriptions = await (
                from item in _unitOfWork.Context.InvoiceItems
                join subscription in _unitOfWork.Context.MemberSubscriptions on item.SubscriptionId equals subscription.Id
                where item.InvoiceId == invoice.Id && item.SubscriptionId != null
                select subscription).ToListAsync(cancellationToken);
            foreach (var subscription in subscriptions)
            {
                subscription.Status = "Refunded";
                subscription.UpdatedAt = now;
            }
        }
    }

    private async Task<InvoiceDetailsResponse> BuildInvoiceDetailsAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var paid = await GetSucceededAmountAsync(invoice.Id, cancellationToken);
        var items = await _unitOfWork.Context.InvoiceItems
            .Where(item => item.InvoiceId == invoice.Id)
            .OrderBy(item => item.Id)
            .Select(item => new InvoiceLineResponse(item.Description, item.Quantity, item.UnitPrice, item.Amount))
            .ToListAsync(cancellationToken);
        var paymentRows = await _unitOfWork.Context.Payments
            .Where(payment => payment.InvoiceId == invoice.Id)
            .OrderBy(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);
        var paymentIds = paymentRows.Select(payment => payment.Id).ToArray();
        var refundRows = await _unitOfWork.Context.PaymentRefunds
            .Where(refund => paymentIds.Contains(refund.PaymentId))
            .OrderBy(refund => refund.CreatedAt)
            .ToListAsync(cancellationToken);
        var payments = paymentRows.Select(payment => new InvoicePaymentResponse(
            payment.Id, payment.PaymentMethod, payment.PaymentStatus, payment.Amount,
            payment.TransactionCode, payment.ProviderTransactionId, payment.CreatedAt, payment.PaidAt,
            refundRows.Where(refund => refund.PaymentId == payment.Id)
                .Select(refund => new InvoiceRefundResponse(refund.Id, refund.Amount, refund.Status,
                    refund.Reason, refund.ProviderRefundId, refund.ExternalReference, refund.CreatedAt, refund.ProcessedAt))
                .ToArray())).ToArray();
        return new InvoiceDetailsResponse(
            invoice.Id, invoice.InvoiceNumber, invoice.MemberId, invoice.CenterId,
            invoice.IssuedAt, invoice.PaidAt, invoice.Status, invoice.Subtotal, invoice.Discount,
            invoice.Tax, invoice.TotalAmount, paid, Math.Max(0m, invoice.TotalAmount - paid), items, payments);
    }

    private async Task<CounterPaymentResponse> BuildCounterResponseAsync(
        Invoice invoice,
        Payment receiptPayment,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Context.MemberProfiles
            .SingleAsync(profile => profile.Id == invoice.MemberId, cancellationToken);
        var item = await _unitOfWork.Context.InvoiceItems
            .SingleAsync(row => row.InvoiceId == invoice.Id && row.PackageId != null, cancellationToken);
        var package = await _unitOfWork.Context.MembershipPackages
            .SingleAsync(candidate => candidate.Id == item.PackageId, cancellationToken);
        var payments = await _unitOfWork.Context.Payments
            .Where(row => row.InvoiceId == invoice.Id && row.PaymentStatus == "Succeeded")
            .OrderByDescending(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        var paid = payments.Sum(row => row.Amount);
        var subscription = item.SubscriptionId.HasValue
            ? await _unitOfWork.Context.MemberSubscriptions.SingleOrDefaultAsync(
                row => row.Id == item.SubscriptionId.Value, cancellationToken)
            : null;
        return new CounterPaymentResponse
        {
            Success = true,
            Message = invoice.Status == "Paid"
                ? "Thanh toán tại quầy thành công."
                : "Đã ghi nhận một phần; gói được kích hoạt khi thu đủ.",
            TransactionRef = receiptPayment.TransactionCode ?? invoice.InvoiceNumber,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceStatus = invoice.Status,
            Amount = invoice.TotalAmount,
            AmountReceived = receiptPayment.AmountReceived ?? receiptPayment.Amount,
            AmountPaid = paid,
            OutstandingBalance = Math.Max(0m, invoice.TotalAmount - paid),
            ChangeDue = Math.Max(0m, (receiptPayment.AmountReceived ?? receiptPayment.Amount) - receiptPayment.Amount),
            PaymentMethod = receiptPayment.PaymentMethod,
            PaidAt = receiptPayment.PaidAt ?? invoice.IssuedAt,
            Member = new MemberReceiptDto
            {
                Id = member.Id,
                FullName = member.FullName,
                MemberCode = member.MemberCode,
                PackageExpiry = subscription?.Status == "Active" ? subscription.EndDate?.ToString("dd/MM/yyyy") ?? string.Empty : string.Empty
            },
            Package = new PackageReceiptDto { Id = package.Id, Name = package.Name, DurationDays = package.DurationDays }
        };
    }

    private async Task SavePaymentLinkAsync(long paymentId, string url, CancellationToken cancellationToken)
    {
        if (url.Length > 2048)
        {
            throw BusinessException.Conflict("URL thanh toán vượt giới hạn lưu trữ.");
        }
        var payment = await _unitOfWork.Context.Payments
            .SingleAsync(candidate => candidate.Id == paymentId, cancellationToken);
        payment.GatewayPaymentUrl = url;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkPaymentPendingForReconciliationAsync(long paymentId, string note)
    {
        var payment = await _unitOfWork.Context.Payments
            .SingleAsync(candidate => candidate.Id == paymentId, CancellationToken.None);
        payment.PaymentStatus = "Pending";
        payment.Note = note;
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SetPaymentStatusAsync(long paymentId, string status, string note, CancellationToken cancellationToken)
    {
        var payment = await _unitOfWork.Context.Payments
            .SingleAsync(candidate => candidate.Id == paymentId, cancellationToken);
        payment.PaymentStatus = status;
        payment.Note = note;
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);
    }

    private async Task<decimal> GetSucceededAmountAsync(long invoiceId, CancellationToken cancellationToken) =>
        await _unitOfWork.Context.Payments
            .Where(payment => payment.InvoiceId == invoiceId && payment.PaymentStatus == "Succeeded")
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;

    private async Task<decimal> GetSuccessfulRefundAmountAsync(long paymentId, CancellationToken cancellationToken) =>
        await _unitOfWork.Context.PaymentRefunds
            .Where(refund => refund.PaymentId == paymentId && refund.Status == "Succeeded")
            .SumAsync(refund => (decimal?)refund.Amount, cancellationToken) ?? 0m;

    private Task<bool> InvoiceHasPackageAsync(long invoiceId, long packageId, CancellationToken cancellationToken) =>
        _unitOfWork.Context.InvoiceItems.AnyAsync(
            item => item.InvoiceId == invoiceId && item.PackageId == packageId, cancellationToken);

    private async Task<string?> FindOpenPendingInvoiceNumberAsync(
        long memberId, long packageId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiryThreshold = now.AddMinutes(-15);

        // Tự động dọn dẹp các hóa đơn chờ thanh toán đã quá hạn (> 15 phút)
        var expiredInvoices = await (
            from item in _unitOfWork.Context.InvoiceItems
            join invoice in _unitOfWork.Context.Invoices on item.InvoiceId equals invoice.Id
            join subscription in _unitOfWork.Context.MemberSubscriptions on item.SubscriptionId equals subscription.Id
            where invoice.MemberId == memberId && item.PackageId == packageId
                && subscription.Status == "PendingPayment"
                && (invoice.Status == "Issued" || invoice.Status == "PartiallyPaid")
                && invoice.IssuedAt < expiryThreshold
            select invoice
        ).Distinct().ToListAsync(cancellationToken);

        if (expiredInvoices.Count > 0)
        {
            foreach (var expiredInv in expiredInvoices)
            {
                await CancelPendingInvoiceCoreAsync(null, expiredInv, now, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await (
            from item in _unitOfWork.Context.InvoiceItems
            join invoice in _unitOfWork.Context.Invoices on item.InvoiceId equals invoice.Id
            join subscription in _unitOfWork.Context.MemberSubscriptions on item.SubscriptionId equals subscription.Id
            where invoice.MemberId == memberId && item.PackageId == packageId
                && subscription.Status == "PendingPayment"
                && (invoice.Status == "Issued" || invoice.Status == "PartiallyPaid")
                && invoice.IssuedAt >= expiryThreshold
            orderby invoice.IssuedAt descending
            select invoice.InvoiceNumber
        ).FirstOrDefaultAsync(cancellationToken);
    }

    private static AuditLog CreateAudit(long? actorId, string action, string entityType, long entityId, object newValues, DateTime createdAt) =>
        new()
        {
            UserId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            NewValues = JsonSerializer.Serialize(newValues),
            CreatedAt = createdAt
        };

    private static PaymentRefundResponse BuildRefundResponse(
        PaymentRefund refund, Payment payment, Invoice invoice, string message) =>
        new(refund.Id, payment.Id, invoice.InvoiceNumber, refund.Amount, refund.Status,
            refund.ProviderRefundId, refund.ExternalReference, message, refund.CreatedAt, refund.ProcessedAt);

    private static PaymentResultResponse PaymentSuccess(Invoice invoice, Payment payment, string message) => new()
    {
        Success = true,
        Processed = true,
        Message = message,
        InvoiceNumber = invoice.InvoiceNumber,
        TransactionId = payment.ProviderTransactionId ?? payment.TransactionCode,
        Amount = payment.Amount
    };

    private static PaymentResultResponse FailedCallback(
        string message,
        string? invoiceNumber = null,
        string providerAckCode = "99") => new()
    {
        Success = false,
        Message = message,
        InvoiceNumber = invoiceNumber,
        ProviderAckCode = providerAckCode
    };

    private static string CreateInvoiceNumber(DateTime now) =>
        $"SC-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30];

    private async Task<PaymentResultResponse> MarkPaymentReviewRequiredAsync(
        Payment payment,
        string provider,
        string reason,
        decimal reportedAmount,
        string? providerTransactionId,
        CancellationToken cancellationToken)
    {
        payment.PaymentStatus = "ReviewRequired";
        payment.PaidAt = null;
        payment.Note = $"ReviewRequired: {reason}; expected={payment.Amount.ToString(CultureInfo.InvariantCulture)}; " +
                       $"reported={reportedAmount.ToString(CultureInfo.InvariantCulture)}";
        _unitOfWork.Context.AuditLogs.Add(CreateAudit(null, "Payment.GatewayReviewRequired", "Payment", payment.Id,
            new
            {
                provider,
                payment.GatewayReference,
                ExpectedAmount = payment.Amount,
                ReportedAmount = reportedAmount,
                Reason = reason,
                ProviderTransactionId = string.IsNullOrWhiteSpace(providerTransactionId)
                    ? null
                    : providerTransactionId.Trim()[..Math.Min(providerTransactionId.Trim().Length, 150)]
            }, DateTime.UtcNow));
        await _unitOfWork.Context.SaveChangesAsync(cancellationToken);

        return new PaymentResultResponse
        {
            Success = false,
            Processed = true,
            Message = "Provider báo đã thu nhưng dữ liệu không khớp; payment được giữ ReviewRequired để đối soát.",
            InvoiceNumber = await _unitOfWork.Context.Invoices
                .Where(invoice => invoice.Id == payment.InvoiceId)
                .Select(invoice => invoice.InvoiceNumber)
                .SingleAsync(cancellationToken),
            TransactionId = string.IsNullOrWhiteSpace(providerTransactionId)
                ? null
                : providerTransactionId.Trim()[..Math.Min(providerTransactionId.Trim().Length, 150)],
            Amount = reportedAmount
        };
    }

    private async Task<PaymentResultResponse> MarkSignedGatewayCallbackReviewRequiredAsync(
        string provider,
        string gatewayReference,
        string reason,
        decimal reportedAmount,
        string? providerTransactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference))
            return FailedCallback("Thiếu mã tham chiếu giao dịch.");

        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var payment = await _unitOfWork.Context.Payments
            .SingleOrDefaultAsync(candidate => candidate.GatewayReference == gatewayReference, cancellationToken);
        if (payment is null || !payment.PaymentMethod.StartsWith(provider, StringComparison.OrdinalIgnoreCase))
            return FailedCallback("Không tìm thấy payment attempt tương ứng.");

        var invoice = await _unitOfWork.Context.Invoices
            .SingleAsync(candidate => candidate.Id == payment.InvoiceId, cancellationToken);
        if (payment.PaymentStatus == "Succeeded")
        {
            await transaction.CommitAsync(cancellationToken);
            return FailedCallback("Payment đã được chốt; callback mâu thuẫn không thể ghi đè giao dịch.", invoice.InvoiceNumber);
        }
        if (payment.PaymentStatus == "ReviewRequired")
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Processed = true,
                Message = "Payment đang ReviewRequired; cần hoàn tất đối soát thủ công.",
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = payment.Amount
            };
        }

        var result = await MarkPaymentReviewRequiredAsync(payment, provider, reason,
            reportedAmount, providerTransactionId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void EnsurePaymentAmount(decimal amount, string fieldName)
    {
        if (amount < 0 || amount > 1_000_000_000m || decimal.Round(amount, 2) != amount)
        {
            throw BusinessException.BadRequest($"{fieldName} phải nằm trong khoảng 0–1.000.000.000 VND và tối đa 2 chữ số thập phân.");
        }
    }

    private static string ScopeIdempotencyKey(long actorId, string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || rawKey.Length > 100)
        {
            throw BusinessException.BadRequest("Header Idempotency-Key là bắt buộc và tối đa 100 ký tự.");
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{actorId}:{rawKey.Trim()}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeProviderTransactionId(string provider, string transactionId) =>
        $"{provider}:{transactionId.Trim()}";

    private static string? NormalizeProviderRefundId(string provider, string? refundId)
    {
        if (string.IsNullOrWhiteSpace(refundId))
        {
            return null;
        }

        var trimmed = refundId.Trim();
        var prefix = provider + ":";
        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? provider + ":" + trimmed[prefix.Length..]
            : provider + ":" + trimmed;
    }

    private static string StripProviderPrefix(string transactionId, string provider)
    {
        var prefix = provider + ":";
        return transactionId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? transactionId[prefix.Length..]
            : transactionId;
    }

    private static string GetProvider(string method) =>
        method.StartsWith("VNPAY", StringComparison.OrdinalIgnoreCase) ? "VNPAY" :
        method.StartsWith("MOMO", StringComparison.OrdinalIgnoreCase) ? "MOMO" :
        method.StartsWith("PAYOS", StringComparison.OrdinalIgnoreCase) ? "PAYOS" : method.ToUpperInvariant();

    private void EnsureGatewayConfiguration(string provider, params string[] keys)
    {
        var missingKeys = keys
            .Where(key =>
            {
                var value = _configuration[key];
                return string.IsNullOrWhiteSpace(value) || value.StartsWith("your-", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();

        if (missingKeys.Length > 0)
        {
            throw new BusinessException(
                HttpStatusCode.ServiceUnavailable,
                $"{provider} thiếu cấu hình bắt buộc: {string.Join(", ", missingKeys)}. Hãy cấu hình bằng User Secrets hoặc biến môi trường.");
        }
    }

    private void EnsurePayOsUrlConfiguration()
    {
        var baseUrl = _configuration["PayOS:BaseUrl"];
        var returnUrl = _configuration["PayOS:ReturnUrl"];
        var cancelUrl = _configuration["PayOS:CancelUrl"];
        var validBaseUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            && baseUri.Scheme == Uri.UriSchemeHttps;
        var validReturnUrl = Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri)
            && returnUri.Scheme is "http" or "https";
        var validCancelUrl = Uri.TryCreate(cancelUrl, UriKind.Absolute, out var cancelUri)
            && cancelUri.Scheme is "http" or "https";

        if (!validBaseUrl || !validReturnUrl || !validCancelUrl)
        {
            throw new BusinessException(
                HttpStatusCode.ServiceUnavailable,
                "PayOS cần BaseUrl HTTPS cùng ReturnUrl và CancelUrl tuyệt đối hợp lệ trước khi tạo payment.");
        }
    }

    private static void EnsureCenterScope(long? allowedCenterId, long invoiceCenterId)
    {
        if (allowedCenterId.HasValue && allowedCenterId.Value != invoiceCenterId)
        {
            throw BusinessException.NotFound("Không tìm thấy dữ liệu trong phạm vi trung tâm của tài khoản.");
        }
    }

    private TimeZoneInfo GetReportTimeZone()
    {
        var configured = _configuration["Reports:TimeZoneId"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configured);
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private DateOnly ToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), GetReportTimeZone()));

    private async Task CancelPendingInvoiceCoreAsync(
        long? actorUserId,
        Invoice invoice,
        DateTime now,
        CancellationToken cancellationToken)
    {
        invoice.Status = "Voided";

        // Void tất cả payment Pending của hóa đơn này
        var pendingPayments = await _unitOfWork.Context.Payments
            .Where(p => p.InvoiceId == invoice.Id && p.PaymentStatus == "Pending")
            .ToListAsync(cancellationToken);
        foreach (var payment in pendingPayments)
        {
            payment.PaymentStatus = "Voided";
            payment.Note = "Hủy hóa đơn chờ thanh toán.";
        }

        // Hủy subscription PendingPayment liên kết
        var invoiceItems = await _unitOfWork.Context.InvoiceItems
            .Where(item => item.InvoiceId == invoice.Id && item.SubscriptionId != null)
            .ToListAsync(cancellationToken);
        foreach (var item in invoiceItems)
        {
            var subscription = await _unitOfWork.Context.MemberSubscriptions
                .SingleOrDefaultAsync(s => s.Id == item.SubscriptionId, cancellationToken);
            if (subscription is not null && subscription.Status == "PendingPayment")
            {
                subscription.Status = "Cancelled";
                subscription.UpdatedAt = now;
            }
        }

        _unitOfWork.Context.AuditLogs.Add(CreateAudit(
            actorUserId, "Invoice.CancelledByMember", "Invoice", invoice.Id,
            new { invoice.InvoiceNumber, reason = "Hủy hóa đơn chờ thanh toán." }, now));
    }

    /// <summary>
    /// Hủy hóa đơn PendingPayment do member chủ động yêu cầu.
    /// Điều kiện: hóa đơn thuộc member đó, chưa có payment Succeeded, trạng thái Issued/PartiallyPaid.
    /// </summary>
    public async Task<PaymentResultResponse> CancelPendingInvoiceAsync(
        long memberUserId,
        string invoiceNumber,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.Context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var invoice = await _unitOfWork.Context.Invoices
            .SingleOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken)
            ?? throw BusinessException.NotFound("Không tìm thấy hóa đơn.");

        // Xác nhận hóa đơn thuộc đúng member đang đăng nhập hoặc nhân viên
        var memberProfile = await _unitOfWork.Context.MemberProfiles
            .SingleOrDefaultAsync(m => m.UserId == memberUserId, cancellationToken);

        if (memberProfile != null && invoice.MemberId != memberProfile.Id)
        {
            var isStaff = await _unitOfWork.Context.StaffProfiles.AnyAsync(s => s.UserId == memberUserId, cancellationToken);
            if (!isStaff)
                throw BusinessException.Forbidden("Bạn không có quyền hủy hóa đơn này.");
        }

        // Không hủy nếu đã có payment thành công
        if (await _unitOfWork.Context.Payments.AnyAsync(
                p => p.InvoiceId == invoice.Id && p.PaymentStatus == "Succeeded", cancellationToken))
        {
            throw BusinessException.Conflict("Hóa đơn đã có thanh toán thành công. Liên hệ nhân viên để được hỗ trợ.");
        }

        if (invoice.Status is "Voided" or "Refunded" or "Paid")
            throw BusinessException.Conflict($"Hóa đơn trạng thái '{invoice.Status}' không thể hủy.");

        if (invoice.Status is not ("Issued" or "PartiallyPaid"))
            throw BusinessException.Conflict($"Chỉ có thể hủy hóa đơn ở trạng thái Issued hoặc PartiallyPaid.");

        var now = DateTime.UtcNow;
        await CancelPendingInvoiceCoreAsync(memberUserId, invoice, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PaymentResultResponse
        {
            Success = true,
            Message = "Đã hủy hóa đơn chờ thanh toán thành công.",
            InvoiceNumber = invoice.InvoiceNumber
        };
    }

    /// <summary>
    /// Hủy hóa đơn chờ thanh toán của gói tập do member hoặc nhân sự chủ động yêu cầu.
    /// </summary>
    public async Task<PaymentResultResponse> CancelPendingPackageAsync(
        long memberUserId,
        long packageId,
        long? targetMemberId = null,
        CancellationToken cancellationToken = default)
    {
        long resolvedMemberId;
        if (targetMemberId.HasValue && targetMemberId.Value > 0)
        {
            resolvedMemberId = targetMemberId.Value;
        }
        else
        {
            var memberProfile = await _unitOfWork.Context.MemberProfiles
                .SingleOrDefaultAsync(m => m.UserId == memberUserId, cancellationToken);
            if (memberProfile is null)
            {
                var isStaff = await _unitOfWork.Context.StaffProfiles.AnyAsync(s => s.UserId == memberUserId, cancellationToken);
                if (!isStaff)
                {
                    throw BusinessException.NotFound("Không tìm thấy hồ sơ thành viên.");
                }

                var anyPending = await (
                    from item in _unitOfWork.Context.InvoiceItems
                    join invoice in _unitOfWork.Context.Invoices on item.InvoiceId equals invoice.Id
                    join subscription in _unitOfWork.Context.MemberSubscriptions on item.SubscriptionId equals subscription.Id
                    where item.PackageId == packageId && subscription.Status == "PendingPayment"
                        && (invoice.Status == "Issued" || invoice.Status == "PartiallyPaid")
                    orderby invoice.IssuedAt descending
                    select invoice.MemberId
                ).FirstOrDefaultAsync(cancellationToken);

                if (anyPending == 0)
                {
                    return new PaymentResultResponse
                    {
                        Success = true,
                        Message = "Không có hóa đơn chờ nào cho gói này."
                    };
                }
                resolvedMemberId = anyPending;
            }
            else
            {
                resolvedMemberId = memberProfile.Id;
            }
        }

        var pendingInvoiceNumber = await FindOpenPendingInvoiceNumberAsync(resolvedMemberId, packageId, cancellationToken);
        if (pendingInvoiceNumber is null)
        {
            return new PaymentResultResponse
            {
                Success = true,
                Message = "Không có hóa đơn chờ nào cho gói này."
            };
        }

        return await CancelPendingInvoiceAsync(memberUserId, pendingInvoiceNumber, cancellationToken);
    }

    /// <summary>
    /// Lấy danh sách hóa đơn đang chờ thanh toán của member.
    /// </summary>
    public async Task<List<PendingInvoiceDto>> GetMyPendingInvoicesAsync(
        long memberUserId,
        CancellationToken cancellationToken = default)
    {
        var memberProfile = await _unitOfWork.Context.MemberProfiles
            .SingleOrDefaultAsync(m => m.UserId == memberUserId, cancellationToken);
        if (memberProfile is null) return new();

        var query = from item in _unitOfWork.Context.InvoiceItems.AsNoTracking()
                    join invoice in _unitOfWork.Context.Invoices.AsNoTracking() on item.InvoiceId equals invoice.Id
                    join subscription in _unitOfWork.Context.MemberSubscriptions.AsNoTracking() on item.SubscriptionId equals subscription.Id
                    join package in _unitOfWork.Context.MembershipPackages.AsNoTracking() on item.PackageId equals package.Id
                    where invoice.MemberId == memberProfile.Id
                        && subscription.Status == "PendingPayment"
                        && (invoice.Status == "Issued" || invoice.Status == "PartiallyPaid")
                    orderby invoice.IssuedAt descending
                    select new
                    {
                        invoice.Id,
                        invoice.InvoiceNumber,
                        PackageId = package.Id,
                        PackageName = package.Name,
                        Amount = invoice.TotalAmount,
                        DurationDays = package.DurationDays,
                        invoice.IssuedAt
                    };

        var items = await query.ToListAsync(cancellationToken);
        if (items.Count == 0) return new();

        var invoiceIds = items.Select(x => x.Id).ToList();
        var latestPayments = await _unitOfWork.Context.Payments.AsNoTracking()
            .Where(p => invoiceIds.Contains(p.InvoiceId))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(x =>
        {
            var p = latestPayments.FirstOrDefault(pay => pay.InvoiceId == x.Id);
            return new PendingInvoiceDto(
                x.InvoiceNumber,
                x.PackageId,
                x.PackageName,
                x.Amount,
                x.DurationDays,
                x.IssuedAt,
                p?.PaymentMethod
            );
        }).ToList();
    }
}

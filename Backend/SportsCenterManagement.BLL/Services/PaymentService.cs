using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Payments;
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
        CreatePaymentRequest request,
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

        await transaction.CommitAsync(cancellationToken);
        var invoiceNumber = invoice.InvoiceNumber;

        // Cấu hình đã được kiểm tra trước khi ghi pending invoice/subscription.
        var vnpay = new VnPayLibrary();

        // Đơn vị tiền tệ của VNPay tính bằng đồng và phải nhân với 100
        var paidAmount = await _unitOfWork.Repository<Payment>()
            .Find(payment => payment.InvoiceId == invoice.Id && payment.PaymentStatus == "Succeeded")
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var amountInVnpayFormat = ((long)((invoice.TotalAmount - paidAmount) * 100)).ToString();

        var clientIp = (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1" || ipAddress.Contains(':')) 
            ? "127.0.0.1" 
            : ipAddress;

        vnpay.AddRequestData("vnp_Version", _configuration["VnPay:Version"] ?? "2.1.0");
        vnpay.AddRequestData("vnp_Command", _configuration["VnPay:Command"] ?? "pay");
        vnpay.AddRequestData("vnp_TmnCode", tmnCode);
        vnpay.AddRequestData("vnp_Amount", amountInVnpayFormat);
        vnpay.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", _configuration["VnPay:CurrCode"] ?? "VND");
        vnpay.AddRequestData("vnp_IpAddr", clientIp);
        vnpay.AddRequestData("vnp_Locale", _configuration["VnPay:Locale"] ?? "vn");
        vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan goi tap {package.Id} - Don hang {invoiceNumber}");
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnpay.AddRequestData("vnp_TxnRef", invoiceNumber);


        // Nếu người dùng có chọn ngân hàng cụ thể từ Frontend
        if (!string.IsNullOrEmpty(request.BankCode))
        {
            vnpay.AddRequestData("vnp_BankCode", request.BankCode);
        }

        // 5. Tạo đường dẫn thanh toán hoàn chỉnh kèm signature SHA512
        return vnpay.CreateRequestUrl(baseUrl, hashSecret);
    }

    /// <summary>
    /// Xử lý dữ liệu trả về sau khi người dùng thực hiện thanh toán trên VNPay:
    /// - Kiểm tra signature hợp lệ để chống giả mạo.
    /// - Cập nhật trạng thái Invoice -> Paid và Subscription -> Active nếu thanh toán thành công (Mã 00).
    /// </summary>
    public async Task<PaymentResultResponse> ProcessPaymentCallbackAsync(
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
            return new PaymentResultResponse
            {
                Success = false,
                Message = "Thiếu chữ ký bảo mật từ VNPay."
            };
        }

        // 1. Kiểm tra signature bảo mật từ VNPay
        var isValidSignature = vnpay.ValidateSignature(vnpSecureHash, hashSecret);
        if (!isValidSignature)
        {
            return new PaymentResultResponse
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
            return new PaymentResultResponse { Success = false, Message = "Dữ liệu thanh toán không hợp lệ." };
        }

        var vnpAmount = amountInVnpayFormat / 100m;
        // Serializable tránh hai callback đồng thời cùng cộng một giao dịch và kích hoạt hai lần.
        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var invoice = await _unitOfWork.Repository<Invoice>()
            .Find(item => item.InvoiceNumber == invoiceNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (invoice is null)
        {
            return new PaymentResultResponse { Success = false, Message = "Không tìm thấy hóa đơn tương ứng với giao dịch." };
        }

        if (!string.IsNullOrWhiteSpace(vnpTransactionNo))
        {
            var existingPayment = await _unitOfWork.Repository<Payment>()
                .Find(item => item.TransactionCode == vnpTransactionNo)
                .SingleOrDefaultAsync(cancellationToken);
            if (existingPayment is not null)
            {
                var sameInvoice = existingPayment.InvoiceId == invoice.Id;
                await transaction.CommitAsync(cancellationToken);
                return new PaymentResultResponse
                {
                    Success = sameInvoice && existingPayment.PaymentStatus == "Succeeded",
                    Message = sameInvoice ? "Giao dịch đã được xử lý." : "Mã giao dịch đã được sử dụng.",
                    InvoiceNumber = invoiceNumber,
                    TransactionId = vnpTransactionNo,
                    Amount = existingPayment.Amount
                };
            }
        }

        if (invoice.Status is "Paid" or "Cancelled")
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentResultResponse
            {
                Success = false,
                Message = invoice.Status == "Paid" ? "Hóa đơn đã được thanh toán bằng giao dịch khác." : "Hóa đơn đã bị hủy.",
                InvoiceNumber = invoiceNumber,
                Amount = vnpAmount
            };
        }

        var invoiceItems = await _unitOfWork.Repository<InvoiceItem>()
            .Find(item => item.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        if (vnpResponseCode == "00")
        {
            if (string.IsNullOrWhiteSpace(vnpTransactionNo))
            {
                await transaction.CommitAsync(cancellationToken);
                return new PaymentResultResponse { Success = false, Message = "Thiếu mã giao dịch thanh toán." };
            }

            var payments = await _unitOfWork.Repository<Payment>()
                .Find(item => item.InvoiceId == invoice.Id && item.PaymentStatus == "Succeeded")
                .ToListAsync(cancellationToken);
            var paidBefore = payments.Sum(item => item.Amount);
            var outstanding = invoice.TotalAmount - paidBefore;

            if (vnpAmount > outstanding || outstanding <= 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return new PaymentResultResponse
                {
                    Success = false,
                    Message = "Số tiền thanh toán vượt quá số dư hóa đơn.",
                    InvoiceNumber = invoiceNumber,
                    TransactionId = vnpTransactionNo,
                    Amount = vnpAmount
                };
            }

            var now = DateTime.UtcNow;
            await _unitOfWork.Repository<Payment>().AddAsync(new Payment
            {
                InvoiceId = invoice.Id,
                MemberId = invoice.MemberId,
                PaymentMethod = "VNPAY",
                TransactionCode = string.IsNullOrWhiteSpace(vnpTransactionNo) ? null : vnpTransactionNo,
                Amount = vnpAmount,
                PaymentStatus = "Succeeded",
                PaidAt = now,
                CreatedAt = now,
                Note = "Thanh toán qua cổng VNPay Sandbox"
            }, cancellationToken);

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
            await transaction.CommitAsync(cancellationToken);

            return new PaymentResultResponse
            {
                Success = true,
                Message = invoice.Status == "Paid" ? "Thanh toán đủ hóa đơn thành công." : "Đã ghi nhận thanh toán một phần; subscription vẫn chờ thanh toán.",
                InvoiceNumber = invoiceNumber,
                TransactionId = vnpTransactionNo,
                Amount = vnpAmount
            };
        }

        await transaction.CommitAsync(cancellationToken);
        return new PaymentResultResponse
        {
            Success = false,
            Message = $"Thanh toán không thành công. Mã lỗi VNPay: {vnpResponseCode}",
            InvoiceNumber = invoiceNumber,
            TransactionId = vnpTransactionNo,
            Amount = vnpAmount
        };
    }
}

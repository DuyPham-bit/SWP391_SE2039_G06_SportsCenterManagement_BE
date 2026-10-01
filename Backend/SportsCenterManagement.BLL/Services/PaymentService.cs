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
        long memberId,
        CreatePaymentRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra Member tồn tại
        var memberExists = await _unitOfWork.Repository<MemberProfile>()
            .AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            throw new InvalidOperationException("Không tìm thấy thông tin thành viên.");
        }

        // 2. Lấy thông tin gói tập đang Active từ Database (Lấy giá gốc niêm yết)
        var package = await _unitOfWork.Repository<MembershipPackage>()
            .Find(p => p.Id == request.PackageId && p.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Gói tập không tồn tại hoặc đã ngừng hoạt động.");

        // 3. Bắt đầu Transaction để lưu Subscription & Invoice tạm thời
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // Tạo bản ghi Subscription với trạng thái PendingPayment
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

        // Tạo mã hóa đơn duy nhất
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

        // Lưu chi tiết hóa đơn
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

        // Commit lưu DB thành công
        await transaction.CommitAsync(cancellationToken);

        // 4. Khởi tạo VNPay Helper và nạp các tham số cấu hình
        var vnpay = new VnPayLibrary();
        var tmnCode = _configuration["VnPay:TmnCode"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:TmnCode");
        var hashSecret = _configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:HashSecret");
        var baseUrl = _configuration["VnPay:BaseUrl"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:BaseUrl");
        var returnUrl = _configuration["VnPay:ReturnUrl"] ?? throw new InvalidOperationException("Chưa cấu hình VnPay:ReturnUrl");

        // Đơn vị tiền tệ của VNPay tính bằng đồng và phải nhân với 100
        var amountInVnpayFormat = ((long)(package.Price * 100)).ToString();

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
        var vnpAmount = Convert.ToDecimal(vnpay.GetResponseData("vnp_Amount")) / 100;

        // 2. Tìm hóa đơn trong Database
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

        // Tìm chi tiết hóa đơn để lấy SubscriptionId liên kết
        var invoiceItem = await _unitOfWork.Repository<InvoiceItem>()
            .Find(item => item.InvoiceId == invoice.Id)
            .SingleOrDefaultAsync(cancellationToken);

        // 3. Kiểm tra mã phản hồi: "00" = Giao dịch thành công
        if (vnpResponseCode == "00")
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Cập nhật Invoice -> Paid
            invoice.Status = "Paid";
            invoice.PaidAt = now;
            _unitOfWork.Repository<Invoice>().Update(invoice);

            // Kích hoạt Subscription -> Active
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

            // Ghi nhận bản ghi Payment thành công
            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                MemberId = invoice.MemberId,
                PaymentMethod = "VNPAY",
                TransactionCode = vnpTransactionNo,
                Amount = vnpAmount,
                PaymentStatus = "Completed",
                PaidAt = now,
                Note = "Thanh toán qua cổng VNPay Sandbox"
            };
            await _unitOfWork.Repository<Payment>().AddAsync(payment, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PaymentResultResponse
            {
                Success = true,
                Message = "Thanh toán gói tập thành công!",
                InvoiceNumber = invoiceNumber,
                TransactionId = vnpTransactionNo,
                Amount = vnpAmount
            };
        }
        else
        {
            // Giao dịch không thành công hoặc người dùng hủy
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
                Message = $"Thanh toán không thành công. Mã lỗi VNPay: {vnpResponseCode}",
                InvoiceNumber = invoiceNumber,
                Amount = vnpAmount
            };
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Authorization;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IMemberService _memberService;
    private readonly ICoreFlowService _coreFlowService;
    private readonly SportsCenterDbContext _dbContext;

    public PaymentsController(
        IPaymentService paymentService,
        ILogger<PaymentsController> logger,
        IMemberService memberService,
        ICoreFlowService coreFlowService,
        SportsCenterDbContext dbContext)
    {
        _paymentService = paymentService;
        _logger = logger;
        _memberService = memberService;
        _coreFlowService = coreFlowService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// API 1: Khởi tạo giao dịch thanh toán VNPay.
    /// </summary>
    [Authorize]
    [HttpPost("create-vnpay-url")]
    public async Task<IActionResult> CreateVnPayPaymentUrl(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return BadRequest(new { success = false, message = "Header Idempotency-Key là bắt buộc." });
            var userId = GetRequiredUserId();
            var memberId = await GetCurrentMemberIdAsync(userId, cancellationToken);
            if (request.PackageId > 0)
            {
                await _memberService.EnsureCanBuyPackageAsync(userId, request.PackageId, cancellationToken);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var paymentUrl = await _paymentService.CreatePaymentUrlAsync(
                memberId, request, ipAddress, idempotencyKey.Trim(), cancellationToken);

            return Ok(new { success = true, paymentUrl });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ hoặc gói tập." });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// API 2: Nhận kết quả thanh toán khi VNPay gọi về (Return URL / Callback).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("vnpay-callback")]
    [ProducesResponseType(typeof(PaymentResultResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentResultResponse>> VnPayPaymentCallback(CancellationToken cancellationToken)
    {
        var result = await _paymentService.ProcessPaymentCallbackAsync(ToStringDictionary(Request.Query), cancellationToken);
        if (!result.Processed) _logger.LogWarning("Rejected VNPay callback. TraceId: {TraceId}", HttpContext.TraceIdentifier);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("vnpay-ipn")]
    public async Task<IActionResult> VnPayPaymentIpn(CancellationToken cancellationToken)
    {
        var result = await _paymentService.ProcessPaymentCallbackAsync(ToStringDictionary(Request.Query), cancellationToken);
        if (!result.Processed) _logger.LogWarning("Rejected VNPay IPN. TraceId: {TraceId}", HttpContext.TraceIdentifier);
        return Ok(new
        {
            RspCode = result.Processed ? "00" : result.ProviderAckCode ?? "99",
            Message = result.Processed ? "Confirm Success" : "Notification was not applied"
        });
    }

    [Authorize]
    [HttpPost("create-momo-url")]
    public async Task<IActionResult> CreateMomoPaymentUrl(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { success = false, message = "Header Idempotency-Key là bắt buộc." });
        var userId = GetRequiredUserId();
        var memberId = await GetCurrentMemberIdAsync(userId, cancellationToken);
        if (request.PackageId > 0)
        {
            await _memberService.EnsureCanBuyPackageAsync(userId, request.PackageId, cancellationToken);
        }
        var paymentUrl = await _paymentService.CreateMomoPaymentUrlAsync(
            memberId, request, idempotencyKey.Trim(), cancellationToken);
        return Ok(new { success = true, paymentUrl });
    }

    [AllowAnonymous]
    [HttpGet("momo-callback")]
    public async Task<ActionResult<PaymentResultResponse>> MomoPaymentCallback(CancellationToken cancellationToken)
    {
        var result = await _paymentService.ProcessMomoCallbackAsync(ToStringDictionary(Request.Query), cancellationToken);
        if (!result.Processed) _logger.LogWarning("Rejected MoMo callback. TraceId: {TraceId}", HttpContext.TraceIdentifier);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("momo-ipn")]
    public async Task<IActionResult> MomoPaymentIpn([FromBody] JsonElement ipnData, CancellationToken cancellationToken)
    {
        if (ipnData.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(new { message = "MoMo IPN body must be a JSON object." });
        }

        var fields = ipnData.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.ToString(),
            StringComparer.Ordinal);
        var result = await _paymentService.ProcessMomoCallbackAsync(fields, cancellationToken);
        if (!result.Processed) _logger.LogWarning("Rejected MoMo IPN. TraceId: {TraceId}", HttpContext.TraceIdentifier);
        return Ok(new { resultCode = result.Processed ? 0 : 1, message = result.Processed ? "Success" : "Not confirmed" });
    }

    [Authorize(Roles = "Receptionist,Manager,Admin")]
    [HttpPost("counter-checkout")]
    public async Task<ActionResult<CounterPaymentResponse>> CounterCheckout(
        [FromBody] CounterPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var staffCenterId = GetCenterScope();
        var result = await _paymentService.ProcessCounterPaymentAsync(
            GetRequiredUserId(), staffCenterId, idempotencyKey, request, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPost("counter-void/{invoiceNumber}")]
    public async Task<ActionResult<PaymentResultResponse>> CounterVoid(
        [FromRoute] string invoiceNumber,
        [FromBody] VoidPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.VoidCounterPaymentAsync(
            GetRequiredUserId(), GetCenterScope(), invoiceNumber, request.Reason, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPost("{gatewayReference}/reconcile")]
    public async Task<ActionResult<PaymentResultResponse>> ReconcilePendingPayment(
        [FromRoute] string gatewayReference,
        [FromBody] ReconcilePendingPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.ReconcilePendingPaymentAsync(
            GetRequiredUserId(), GetCenterScope(), gatewayReference, request, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPost("{paymentId:long}/refunds")]
    public async Task<ActionResult<PaymentRefundResponse>> Refund(
        [FromRoute] long paymentId,
        [FromBody] CreateRefundRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.RefundPaymentAsync(
            GetRequiredUserId(), GetCenterScope(), paymentId, idempotencyKey, request,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPost("refunds/{refundId:long}/reconcile")]
    public async Task<ActionResult<PaymentRefundResponse>> ReconcileRefund(
        [FromRoute] long refundId,
        [FromBody] ReconcileRefundRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.ReconcileRefundAsync(
            GetRequiredUserId(), GetCenterScope(), refundId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/invoices/{invoiceId:long}/payments")]
    [RequirePermission(PermissionCodes.PaymentCenterCash)]
    public async Task<IActionResult> RecordCashPayment(
        long invoiceId,
        [FromBody] SportsCenterManagement.BLL.DTOs.Payments.Requests.RecordCashPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actorId = GetRequiredUserId();
            await _memberService.EnsureCanProcessInvoiceAsync(actorId, invoiceId, cancellationToken);
            var payment = await _coreFlowService.RecordCashPaymentAsync(
                invoiceId, actorId, request.Amount, request.IdempotencyKey, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new SportsCenterManagement.BLL.DTOs.Payments.Responses.CashPaymentResponse(
                payment.Id, payment.InvoiceId, payment.Amount, payment.PaymentStatus,
                payment.PaidAt ?? throw new InvalidOperationException("Cash payment success has no paid timestamp.")));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private async Task<long> GetCurrentMemberIdAsync(long userId, CancellationToken cancellationToken)
    {
        return await _dbContext.MemberProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Không tìm thấy hồ sơ thành viên cho tài khoản hiện tại.");
    }

    private long GetRequiredUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            throw new InvalidOperationException("JWT is missing a valid user id claim.");
        }
        return id;
    }

    private long? GetCenterScope()
    {
        if (User.IsInRole("Admin"))
        {
            return null;
        }
        var value = User.FindFirstValue("centerId");
        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
        {
            return id;
        }
        throw BusinessException.Forbidden("Tài khoản nhân viên chưa được gán vào trung tâm hoạt động.");
    }

    private static IDictionary<string, string> ToStringDictionary(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
}

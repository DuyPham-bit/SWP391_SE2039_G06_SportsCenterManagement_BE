using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger) : ControllerBase
{
    [Authorize(Roles = "Member")]
    [HttpPost("create-vnpay-url")]
    public async Task<IActionResult> CreateVnPayPaymentUrl(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var memberId = GetRequiredClaimLong("memberId");
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var paymentUrl = await paymentService.CreatePaymentUrlAsync(
            memberId, request, ipAddress, idempotencyKey, cancellationToken);
        return Ok(new { success = true, paymentUrl });
    }

    [AllowAnonymous]
    [HttpGet("vnpay-callback")]
    public async Task<ActionResult<PaymentResultResponse>> VnPayPaymentCallback(CancellationToken cancellationToken)
    {
        var result = await paymentService.ProcessPaymentCallbackAsync(ToStringDictionary(Request.Query), cancellationToken);
        if (!result.Processed) logger.LogWarning("Rejected VNPay callback. TraceId: {TraceId}", HttpContext.TraceIdentifier);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("vnpay-ipn")]
    public async Task<IActionResult> VnPayPaymentIpn(CancellationToken cancellationToken)
    {
        var result = await paymentService.ProcessPaymentCallbackAsync(ToStringDictionary(Request.Query), cancellationToken);
        if (!result.Processed) logger.LogWarning("Rejected VNPay IPN. TraceId: {TraceId}", HttpContext.TraceIdentifier);
        return Ok(new
        {
            RspCode = result.Processed ? "00" : result.ProviderAckCode ?? "99",
            Message = result.Processed ? "Confirm Success" : "Notification was not applied"
        });
    }

    [Authorize(Roles = "Member")]
    [HttpPost("create-momo-url")]
    public async Task<IActionResult> CreateMomoPaymentUrl(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var memberId = GetRequiredClaimLong("memberId");
        var paymentUrl = await paymentService.CreateMomoPaymentUrlAsync(
            memberId, request, idempotencyKey, cancellationToken);
        return Ok(new { success = true, paymentUrl });
    }

    [AllowAnonymous]
    [HttpGet("momo-callback")]
    public async Task<ActionResult<PaymentResultResponse>> MomoPaymentCallback(CancellationToken cancellationToken)
    {
        var result = await paymentService.ProcessMomoCallbackAsync(ToStringDictionary(Request.Query), cancellationToken);
        if (!result.Processed) logger.LogWarning("Rejected MoMo callback. TraceId: {TraceId}", HttpContext.TraceIdentifier);
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
        var result = await paymentService.ProcessMomoCallbackAsync(fields, cancellationToken);
        if (!result.Processed) logger.LogWarning("Rejected MoMo IPN. TraceId: {TraceId}", HttpContext.TraceIdentifier);
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
        var result = await paymentService.ProcessCounterPaymentAsync(
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
        var result = await paymentService.VoidCounterPaymentAsync(
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
        var result = await paymentService.ReconcilePendingPaymentAsync(
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
        var result = await paymentService.RefundPaymentAsync(
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
        var result = await paymentService.ReconcileRefundAsync(
            GetRequiredUserId(), GetCenterScope(), refundId, request, cancellationToken);
        return Ok(result);
    }

    private long GetRequiredUserId() => GetRequiredClaimLong(ClaimTypes.NameIdentifier);

    private long GetRequiredClaimLong(string claimType)
    {
        var value = User.FindFirstValue(claimType);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            throw new InvalidOperationException($"JWT is missing a valid {claimType} claim.");
        }
        return id;
    }

    private long? GetCenterScope()
    {
        if (User.IsInRole("Admin"))
        {
            return null;
        }
        return GetRequiredClaimLong("centerId");
    }

    private static IDictionary<string, string> ToStringDictionary(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
}

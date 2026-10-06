using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller tiếp nhận các yêu cầu thanh toán từ Frontend và xử lý phản hồi từ VNPay.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IMemberService? _memberService;
    private readonly ICoreFlowService? _coreFlowService;

    public PaymentsController(
        IPaymentService paymentService,
        IMemberService? memberService = null,
        ICoreFlowService? coreFlowService = null)
    {
        _paymentService = paymentService;
        _memberService = memberService;
        _coreFlowService = coreFlowService;
    }

    /// <summary>
    /// API 1: Khởi tạo giao dịch thanh toán gói tập.
    /// </summary>
    [HttpPost("create-vnpay-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePaymentUrl(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "X-Member-Id")] long? memberIdHeader,
        CancellationToken cancellationToken)
    {
        try
        {
            long actorId = 1;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var parsedId))
            {
                actorId = parsedId;
            }
            else if (memberIdHeader.HasValue)
            {
                actorId = memberIdHeader.Value;
            }

            if (_memberService is not null && User.Identity?.IsAuthenticated == true)
            {
                await _memberService.EnsureCanBuyPackageAsync(actorId, request.PackageId, cancellationToken);
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var paymentUrl = await _paymentService.CreatePaymentUrlAsync(
                actorId,
                request,
                ipAddress,
                cancellationToken);

            return Ok(new
            {
                success = true,
                paymentUrl = paymentUrl
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ hoặc gói tập." });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// API 2: Nhận kết quả thanh toán khi VNPay gọi về (Return URL / Callback).
    /// </summary>
    [HttpGet("vnpay-callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentResultResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentResultResponse>> PaymentCallback(CancellationToken cancellationToken)
    {
        var queryDictionary = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _paymentService.ProcessPaymentCallbackAsync(queryDictionary, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/invoices/{invoiceId:long}/payments")]
    [RequirePermission(PermissionCodes.PaymentCenterCash)]
    [ProducesResponseType(typeof(Responses.CashPaymentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordCashPayment(
        long invoiceId,
        [FromBody] Requests.RecordCashPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (_coreFlowService is null || _memberService is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        try
        {
            var actorId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _memberService.EnsureCanProcessInvoiceAsync(actorId, invoiceId, cancellationToken);
            var payment = await _coreFlowService.RecordCashPaymentAsync(
                invoiceId, actorId, request.Amount, request.IdempotencyKey, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new Responses.CashPaymentResponse(
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
}

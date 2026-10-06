using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.DAL.Authorization;
using PaymentsRequests = SportsCenterManagement.BLL.DTOs.Payments.Requests;
using PaymentsResponses = SportsCenterManagement.BLL.DTOs.Payments.Responses;
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
    private readonly IMemberService _memberService;
    private readonly ICoreFlowService _coreFlowService;

    public PaymentsController(
        IPaymentService paymentService,
        IMemberService memberService,
        ICoreFlowService coreFlowService)
    {
        _paymentService = paymentService;
        _memberService = memberService;
        _coreFlowService = coreFlowService;
    }

    /// <summary>
    /// API 1: Khởi tạo giao dịch thanh toán gói tập.
    /// Frontend gọi API này khi Member bấm nút "Thanh toán gói".
    /// </summary>
    /// <param name="request">Chứa PackageId và BankCode (tùy chọn)</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Trả về đường dẫn URL của VNPay để Frontend chuyển trang</returns>
    [HttpPost("create-vnpay-url")]
    [RequirePermission(PermissionCodes.PaymentSelfCreate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePaymentUrl(
        [FromBody] PaymentsRequests.CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, message = "Cần đăng nhập để tạo yêu cầu thanh toán." });
            }
            await _memberService.EnsureCanBuyPackageAsync(userId, request.PackageId, cancellationToken);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var paymentUrl = await _paymentService.CreatePaymentUrlAsync(
                userId,
                request,
                ipAddress,
                cancellationToken);

            return Ok(new
            {
                success = true,
                paymentUrl = paymentUrl
            });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new
            {
                success = false,
                message = "Không thể tạo yêu cầu thanh toán với thông tin hiện tại."
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
    }

    /// <summary>
    /// API 2: Nhận kết quả thanh toán khi VNPay gọi về (Return URL / Callback).
    /// </summary>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Kết quả giao dịch chi tiết (Thành công / Thất bại)</returns>
    [HttpGet("vnpay-callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentsResponses.PaymentResultResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentsResponses.PaymentResultResponse>> PaymentCallback(CancellationToken cancellationToken)
    {
        var queryDictionary = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _paymentService.ProcessPaymentCallbackAsync(queryDictionary, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/invoices/{invoiceId:long}/payments")]
    [RequirePermission(PermissionCodes.PaymentCenterCash)]
    [ProducesResponseType(typeof(PaymentsResponses.CashPaymentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordCashPayment(
        long invoiceId,
        PaymentsRequests.RecordCashPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actorId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _memberService.EnsureCanProcessInvoiceAsync(actorId, invoiceId, cancellationToken);
            var payment = await _coreFlowService.RecordCashPaymentAsync(
                invoiceId, actorId, request.Amount, request.IdempotencyKey, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new PaymentsResponses.CashPaymentResponse(
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

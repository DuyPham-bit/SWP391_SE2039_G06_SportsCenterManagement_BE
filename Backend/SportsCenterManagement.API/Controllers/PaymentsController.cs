using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller tiếp nhận các yêu cầu thanh toán (VNPay, MoMo) từ Frontend và xử lý callback.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    #region VNPay Endpoints

    /// <summary>
    /// API 1: Khởi tạo giao dịch thanh toán gói tập qua VNPay.
    /// </summary>
    [HttpPost("create-vnpay-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateVnPayPaymentUrl(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "X-Member-Id")] long? memberIdHeader,
        CancellationToken cancellationToken)
    {
        try
        {
            var memberId = GetCurrentMemberId(memberIdHeader);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var paymentUrl = await _paymentService.CreatePaymentUrlAsync(
                memberId,
                request,
                ipAddress,
                cancellationToken);

            return Ok(new
            {
                success = true,
                paymentUrl = paymentUrl
            });
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
    /// API 2: Nhận kết quả thanh toán từ VNPay gọi về (Return URL / Callback).
    /// </summary>
    [HttpGet("vnpay-callback")]
    [ProducesResponseType(typeof(PaymentResultResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentResultResponse>> VnPayPaymentCallback(CancellationToken cancellationToken)
    {
        var queryDictionary = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _paymentService.ProcessPaymentCallbackAsync(queryDictionary, cancellationToken);
        return Ok(result);
    }

    #endregion

    #region MoMo Endpoints

    /// <summary>
    /// API 3: Khởi tạo giao dịch thanh toán gói tập qua ví điện tử MoMo.
    /// </summary>
    /// <param name="request">Chứa PackageId cần mua</param>
    /// <param name="memberIdHeader">MemberId tạm thời (Header X-Member-Id hoặc từ JWT)</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Trả về paymentUrl của MoMo Sandbox để người dùng quét QR / thanh toán</returns>
    [HttpPost("create-momo-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMomoPaymentUrl(
        [FromBody] CreatePaymentRequest request,
        [FromHeader(Name = "X-Member-Id")] long? memberIdHeader,
        CancellationToken cancellationToken)
    {
        try
        {
            var memberId = GetCurrentMemberId(memberIdHeader);

            var paymentUrl = await _paymentService.CreateMomoPaymentUrlAsync(
                memberId,
                request,
                cancellationToken);

            return Ok(new
            {
                success = true,
                paymentUrl = paymentUrl
            });
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
    /// API 4: Nhận kết quả thanh toán từ MoMo gọi về (Return URL / Callback trên trình duyệt).
    /// </summary>
    [HttpGet("momo-callback")]
    [ProducesResponseType(typeof(PaymentResultResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentResultResponse>> MomoPaymentCallback(CancellationToken cancellationToken)
    {
        var queryDictionary = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _paymentService.ProcessMomoCallbackAsync(queryDictionary, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// API 5: Nhận Webhook IPN (Server-to-Server) tự động từ MoMo.
    /// </summary>
    [HttpPost("momo-ipn")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MomoPaymentIpn(
        [FromBody] IDictionary<string, string> ipnData,
        CancellationToken cancellationToken)
    {
        await _paymentService.ProcessMomoCallbackAsync(ipnData, cancellationToken);
        return NoContent();
    }

    #endregion


    #region Counter / Cash Payment Endpoints

    /// <summary>
    /// API 6: Tiếp nhận và xác thực thanh toán tại quầy (Tiền mặt / POS) từ Lễ tân, kích hoạt ngay gói tập.
    /// </summary>
    /// <param name="request">Chứa MemberId, PackageId, PaymentMethod, AmountReceived...</param>
    /// <param name="staffIdHeader">Staff/User ID của Lễ tân (Lấy tự động từ JWT Token hoặc Header X-Staff-Id khi test)</param>
    /// <param name="cancellationToken">Token hủy request</param>
    [HttpPost("counter-checkout")]
    [ProducesResponseType(typeof(CounterPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CounterCheckout(
        [FromBody] CounterPaymentRequest request,
        [FromHeader(Name = "X-Staff-Id")] long? staffIdHeader,
        CancellationToken cancellationToken)
    {
        try
        {
            // Lấy ID nhân viên thu ngân đang đăng nhập
            var staffUserId = GetCurrentStaffUserId(staffIdHeader);

            var result = await _paymentService.ProcessCounterPaymentAsync(staffUserId, request, cancellationToken);
            return Ok(result);
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
    /// API 7: Hủy giao dịch thanh toán nhầm tại quầy (Void Transaction & Thu hồi gói tập).
    /// </summary>
    /// <param name="invoiceNumber">Mã hóa đơn cần hủy (VD: SC-20261002-XXXX)</param>
    /// <param name="request">Lý do giải trình hủy</param>
    /// <param name="staffIdHeader">Staff/User ID của người thực hiện</param>
    /// <param name="cancellationToken">Token hủy request</param>
    [HttpPost("counter-void/{invoiceNumber}")]
    [ProducesResponseType(typeof(PaymentResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CounterVoid(
        [FromRoute] string invoiceNumber,
        [FromBody] VoidPaymentRequest request,
        [FromHeader(Name = "X-Staff-Id")] long? staffIdHeader,
        CancellationToken cancellationToken)
    {
        try
        {
            var staffUserId = GetCurrentStaffUserId(staffIdHeader);
            var result = await _paymentService.VoidCounterPaymentAsync(staffUserId, invoiceNumber, request.Reason, cancellationToken);
            return Ok(result);
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

    #endregion


    #region Helper Methods

    private long GetCurrentMemberId(long? memberIdHeader)
    {
        var memberIdClaim = User.FindFirst("memberId")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(memberIdClaim) && long.TryParse(memberIdClaim, out var parsedMemberId))
        {
            return parsedMemberId;
        }

        if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var parsedId))
        {
            return parsedId;
        }

        if (memberIdHeader.HasValue)
        {
            return memberIdHeader.Value;
        }

        return 1; // Fallback mặc định khi test trên Swagger không truyền header
    }

    private long GetCurrentStaffUserId(long? staffIdHeader)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("userId")?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var parsedUserId))
        {
            return parsedUserId;
        }

        if (staffIdHeader.HasValue)
        {
            return staffIdHeader.Value;
        }

        return 1; // Fallback ID mặc định (Lễ tân/Admin ID = 1) khi test trên Swagger không truyền header
    }


    #endregion
}

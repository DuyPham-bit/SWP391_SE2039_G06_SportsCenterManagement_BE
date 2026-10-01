using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
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

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// API 1: Khởi tạo giao dịch thanh toán gói tập.
    /// Frontend gọi API này khi Member bấm nút "Thanh toán gói".
    /// </summary>
    /// <param name="request">Chứa PackageId và BankCode (tùy chọn)</param>
    /// <param name="memberIdHeader">MemberId tạm thời (dùng header X-Member-Id hoặc lấy từ JWT)</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Trả về đường dẫn URL của VNPay để Frontend chuyển trang</returns>
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
            // Lấy MemberId: Ưu tiên lấy từ JWT Claims (claim 'memberId' hoặc NameIdentifier), hoặc fallback lấy từ Header X-Member-Id
            long memberId = 1; // Giá trị mặc định khi test trực tiếp trên Swagger không truyền header
            var memberIdClaim = User.FindFirst("memberId")?.Value;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(memberIdClaim) && long.TryParse(memberIdClaim, out var parsedMemberId))
            {
                memberId = parsedMemberId;
            }
            else if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var parsedId))
            {
                memberId = parsedId;
            }
            else if (memberIdHeader.HasValue)
            {
                memberId = memberIdHeader.Value;
            }

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
    /// API 2: Nhận kết quả thanh toán khi VNPay gọi về (Return URL / Callback).
    /// </summary>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Kết quả giao dịch chi tiết (Thành công / Thất bại)</returns>
    [HttpGet("vnpay-callback")]
    [ProducesResponseType(typeof(PaymentResultResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentResultResponse>> PaymentCallback(CancellationToken cancellationToken)
    {
        var queryDictionary = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var result = await _paymentService.ProcessPaymentCallbackAsync(queryDictionary, cancellationToken);
        return Ok(result);
    }
}


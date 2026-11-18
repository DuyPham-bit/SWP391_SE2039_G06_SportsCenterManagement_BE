using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Common;

namespace SportsCenterManagement.API.Controllers.Payments;

[ApiController]
[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController(IPaymentService paymentService, CenterAccessGuard centerAccess) : ControllerBase
{
    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [CenterCapability("register_counter_package")]
    [HttpGet("/api/centers/{centerId:long}/invoices")]
    public async Task<IActionResult> List(long centerId, CancellationToken ct, [FromQuery] string? query = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        await CenterScope.EnsureCenterAccessAsync(centerAccess, User, centerId, ct);
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100 || query?.Length > 200)
            return BadRequest(new { message = "Bộ lọc hoặc phân trang không hợp lệ." });
        return Ok(await paymentService.ListCenterInvoicesAsync(centerId, query, page, pageSize, ct));
    }

    [HttpGet("{invoiceNumber}")]
    [ProducesResponseType(typeof(InvoiceDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDetailsResponse>> GetInvoice(
        [FromRoute] string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var details = await paymentService.GetInvoiceAsync(
            GetRequiredUserId(),
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            GetCenterScope(),
            invoiceNumber,
            cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [HttpPost("{invoiceNumber}/payments")]
    [CenterCapability("register_counter_package")]
    [ProducesResponseType(typeof(InvoiceDetailsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceDetailsResponse>> RecordPayment(
        [FromRoute] string invoiceNumber,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] RecordInvoicePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var details = await paymentService.RecordInvoicePaymentAsync(
            GetRequiredUserId(), GetCenterScope(), invoiceNumber, idempotencyKey, request, cancellationToken);
        return Ok(details);
    }

    private long GetRequiredUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            throw new InvalidOperationException("JWT is missing a valid user id claim.");
        }
        return userId;
    }

    private long? GetCenterScope()
    {
        if (User.IsInRole("ADMIN") || User.IsInRole("MEMBER"))
        {
            return null;
        }
        var value = User.FindFirstValue("centerId");
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var centerId) || centerId <= 0)
        {
        throw BusinessException.Forbidden("Tài khoản nhân viên chưa được gán vào trung tâm hoạt động.");
        }
        return centerId;
    }
}

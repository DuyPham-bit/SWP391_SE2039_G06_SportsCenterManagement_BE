using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Common;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public sealed class InvoicesController(IPaymentService paymentService) : ControllerBase
{
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

    [Authorize(Roles = "Receptionist,Manager,Admin")]
    [HttpPost("{invoiceNumber}/payments")]
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
        if (User.IsInRole("Admin") || User.IsInRole("Member"))
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

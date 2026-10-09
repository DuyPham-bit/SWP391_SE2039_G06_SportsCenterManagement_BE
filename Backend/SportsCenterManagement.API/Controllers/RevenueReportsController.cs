using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Reports;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/revenue")]
public sealed class RevenueReportsController(IRevenueReportService reportService) : ControllerBase
{
    [HttpGet("details")]
    [ProducesResponseType(typeof(DetailedRevenueReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DetailedRevenueReportResponse>> GetRevenue(
        [FromQuery] long centerId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? groupBy, [FromQuery] bool includeTransactions, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId)) return Unauthorized();
        if (centerId <= 0) return BadRequest(new { message = "centerId is required." });
        var today = VietnamTime.GetDate(DateTime.UtcNow);
        try
        {
            return Ok(await reportService.GetRevenueReportAsync(userId, centerId, from ?? today.AddDays(-29),
                to ?? today, groupBy, includeTransactions, cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

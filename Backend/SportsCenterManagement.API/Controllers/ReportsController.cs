using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.CoreFlows;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "MANAGER,ADMIN")]
public sealed class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueReportResponse>> GetRevenue(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string groupBy = "day",
        [FromQuery] long? centerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCenterScope(centerId, out var scopedCenterId)) return CenterScopeFailure();

        var report = await reportService.GetRevenueReportAsync(
            scopedCenterId, from, to, groupBy, cancellationToken);
        return Ok(report);
    }

    [HttpGet("membership")]
    [ProducesResponseType(typeof(MembershipReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MembershipReportResponse>> GetMembership(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string groupBy = "day",
        [FromQuery] long? centerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCenterScope(centerId, out var scopedCenterId)) return CenterScopeFailure();
        return Ok(await reportService.GetMembershipReportAsync(scopedCenterId, from, to, groupBy, cancellationToken));
    }

    [HttpGet("classes")]
    [ProducesResponseType(typeof(ClassEnrollmentReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassEnrollmentReportResponse>> GetClasses(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string groupBy = "day",
        [FromQuery] long? centerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCenterScope(centerId, out var scopedCenterId)) return CenterScopeFailure();
        return Ok(await reportService.GetClassEnrollmentReportAsync(scopedCenterId, from, to, groupBy, cancellationToken));
    }

    private bool TryGetCenterScope(long? requestedCenterId, out long scopedCenterId)
    {
        if (User.IsInRole("Admin"))
        {
            scopedCenterId = requestedCenterId.GetValueOrDefault();
            return scopedCenterId > 0;
        }

        var claim = User.FindFirst("centerId")?.Value;
        return long.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out scopedCenterId) && scopedCenterId > 0;
    }

    private ActionResult CenterScopeFailure() => User.IsInRole("Admin")
        ? BadRequest(new { message = "Admin phải chỉ định centerId." })
        : Forbid();
}

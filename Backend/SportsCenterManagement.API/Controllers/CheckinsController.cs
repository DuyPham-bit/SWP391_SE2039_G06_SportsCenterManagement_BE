using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Checkins;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/centers/{centerId:long}/checkins")]
public sealed class CheckinsController(ICheckinService checkinService) : ControllerBase
{
    [HttpGet("eligibility")]
    [ProducesResponseType(typeof(CheckinEligibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CheckinEligibilityResponse>> GetEligibility(
        long centerId,
        [FromQuery] long? memberId,
        [FromQuery] string? memberCode,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var staffUserId))
        {
            return Unauthorized(new { success = false, message = "Staff identity is required." });
        }

        try
        {
            var result = await checkinService.GetEligibilityAsync(
                staffUserId,
                centerId,
                memberId,
                memberCode,
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(CounterCheckinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CounterCheckinResponse>> CheckIn(
        long centerId,
        [FromBody] CounterCheckinRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var staffUserId))
        {
            return Unauthorized(new { success = false, message = "Staff identity is required." });
        }

        if (request is null)
        {
            return BadRequest(new { success = false, message = "Request body is required." });
        }

        try
        {
            var result = await checkinService.CheckInAsync(
                staffUserId,
                centerId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CheckinListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CheckinListItemResponse>>> GetDailyCheckins(
        long centerId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var staffUserId))
        {
            return Unauthorized(new { success = false, message = "Staff identity is required." });
        }

        try
        {
            var businessDate = date ?? VietnamTime.GetDate(DateTime.UtcNow);
            var result = await checkinService.GetDailyCheckinsAsync(
                staffUserId,
                centerId,
                businessDate,
                cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
    }

}

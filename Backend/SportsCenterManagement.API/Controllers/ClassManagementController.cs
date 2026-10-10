using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

/// <summary>UC-12 Manage Classes &amp; Rooms (Manager/Admin only).</summary>
[Authorize(Roles = "MANAGER,ADMIN")]
public sealed class ClassManagementController(IClassManagementService service, SportsCenterDbContext db) : CenterScopedControllerBase(db)
{
    [HttpPost("classes")]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest request, CancellationToken ct)
    {
        if (!await CanAccessCenterAsync(request.CenterId, ct)) return NotFound();
        var result = await service.CreateClassAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { classId = result.Id }, result);
    }

    [HttpGet("classes/{classId:long}")]
    public async Task<IActionResult> Get(long classId, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return Ok(await service.GetClassAsync(classId, ct));
    }

    [HttpPut("classes/{classId:long}")]
    public async Task<IActionResult> Update(long classId, [FromBody] UpdateClassRequest request, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return Ok(await service.UpdateClassAsync(classId, request, ct));
    }

    /// <summary>Soft cancel; the response reports how many members/bookings were affected.</summary>
    [HttpDelete("classes/{classId:long}")]
    public async Task<IActionResult> Cancel(long classId, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return Ok(await service.CancelClassAsync(classId, ct, CurrentUserId));
    }

    [HttpGet("classes/{classId:long}/schedules")]
    public async Task<IActionResult> GetSchedules(long classId, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return Ok(await service.GetSchedulesAsync(classId, ct));
    }

    [HttpPost("classes/{classId:long}/schedules")]
    public async Task<IActionResult> CreateSchedule(long classId, [FromBody] CreateClassScheduleRequest request, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return StatusCode(StatusCodes.Status201Created, await service.CreateScheduleAsync(classId, request, ct));
    }

    [HttpPut("classes/{classId:long}/schedules/{scheduleId:long}")]
    public async Task<IActionResult> UpdateSchedule(long classId, long scheduleId, [FromBody] UpdateClassScheduleRequest request, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return Ok(await service.UpdateScheduleAsync(classId, scheduleId, request, ct, CurrentUserId));
    }

    [HttpDelete("classes/{classId:long}/schedules/{scheduleId:long}")]
    public async Task<IActionResult> CancelSchedule(long classId, long scheduleId, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return Ok(await service.CancelScheduleAsync(classId, scheduleId, ct, CurrentUserId));
    }

    [HttpPost("classes/{classId:long}/publish")]
    public async Task<IActionResult> Publish(long classId, CancellationToken ct)
    {
        if (!await CanAccessClassAsync(classId, ct)) return NotFound();
        return Ok(await service.PublishClassAsync(classId, ct));
    }
}

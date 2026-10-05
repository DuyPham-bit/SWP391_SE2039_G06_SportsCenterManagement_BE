using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>UC-42 View Class Schedules and UC-30 View Teaching Schedule.</summary>
public sealed class ClassSchedulesController(IClassScheduleQueryService service) : FlowControllerBase
{
    /// <summary>Malformed dates/pagination are rejected with 400 before any query runs.</summary>
    [Authorize(Roles = "MEMBER,MANAGER,ADMIN,RECEPTIONIST")]
    [HttpGet("class-sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] long? sportId,
        [FromQuery] long? coachId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await service.GetSessionsAsync(new SessionScheduleQuery(sportId, coachId, from, to, page, pageSize), ct));

    /// <summary>No coach id parameter on purpose: the coach is taken from the token.</summary>
    [Authorize(Roles = "COACH")]
    [HttpGet("coaches/me/teaching-schedule")]
    public async Task<IActionResult> GetTeachingSchedule(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await service.GetTeachingScheduleAsync(CurrentUserId, from, to, ct));
}

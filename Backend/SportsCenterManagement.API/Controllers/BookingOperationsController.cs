using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
public sealed class BookingOperationsController(ISessionBookingService bookings) : FlowControllerBase, IAsyncActionFilter
{
    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [HttpGet("members/{memberId:long}/session-bookings")]
    [CenterCapability("lookup_member")]
    public async Task<IActionResult> ListForMember(long memberId, CancellationToken ct)
    {
        var userId = await bookings.ResolveMemberUserIdAsync(CurrentUserId, User.IsInRole("ADMIN"), memberId, ct);
        return Ok(await bookings.ListMyBookingsAsync(userId, ct));
    }

    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [HttpPost("members/{memberId:long}/class-sessions/{sessionId:long}/bookings")]
    [CenterCapability("lookup_member")]
    public async Task<IActionResult> BookForMember(long memberId, long sessionId, CancellationToken ct)
    {
        var result = await bookings.BookForMemberAsync(CurrentUserId, User.IsInRole("ADMIN"), memberId, sessionId, ct);
        return StatusCode(result.Booked ? 201 : 202, result);
    }

    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [HttpDelete("members/{memberId:long}/session-bookings/{bookingId:long}")]
    [CenterCapability("lookup_member")]
    public async Task<IActionResult> CancelForMember(long memberId, long bookingId, CancellationToken ct)
    {
        var result = await bookings.CancelForMemberAsync(CurrentUserId, User.IsInRole("ADMIN"), memberId, bookingId, ct);
        return Ok(result);
    }

    [Authorize(Roles = "MEMBER")]
    [HttpGet("members/me/session-waitlist")]
    [CenterCapability("book_class")]
    public async Task<IActionResult> Waitlist(CancellationToken ct) =>
        Ok(await bookings.GetMyWaitlistAsync(CurrentUserId, ct));

    [Authorize(Roles = "MEMBER")]
    [HttpDelete("members/me/session-waitlist/{waitlistId:long}")]
    [CenterCapability("cancel_booking")]
    public async Task<IActionResult> LeaveWaitlist(long waitlistId, CancellationToken ct)
    {
        await bookings.LeaveWaitlistAsync(CurrentUserId, waitlistId, ct);
        return NoContent();
    }

    [Authorize(Roles = "COACH")]
    [HttpGet("coaches/me/sessions/{sessionId:long}/members")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Roster(long sessionId, CancellationToken ct) =>
        Ok(await bookings.GetSessionRosterAsync(CurrentUserId, sessionId, ct));

    public sealed record AttendanceItem(long MemberId, string Status, string? Note);

    [Authorize(Roles = "COACH")]
    [HttpPut("coaches/me/sessions/{sessionId:long}/attendance")]
    [CenterCapability("take_attendance")]
    public async Task<IActionResult> Attendance(long sessionId, AttendanceItem[] records, CancellationToken ct)
    {
        var dtos = records.Select(r => new AttendanceRecordDto(r.MemberId, r.Status, r.Note)).ToList();
        var count = await bookings.RecordAttendanceAsync(CurrentUserId, sessionId, dtos, ct);
        return Ok(new { Count = count });
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await next();
    }
}

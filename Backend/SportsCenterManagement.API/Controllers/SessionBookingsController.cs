using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>UC-43 Book Class and UC-44 Cancel Class Booking.</summary>
public sealed class SessionBookingsController(ISessionBookingService service) : FlowControllerBase
{
    [Authorize(Roles = "MEMBER")]
    [HttpGet("members/me/session-bookings")]
    public async Task<IActionResult> GetMine(CancellationToken ct) =>
        Ok(await service.ListMyBookingsAsync(CurrentUserId, ct));

    /// <summary>201 when booked; 202 when the session was full and the member joined the waitlist.</summary>
    [Authorize(Roles = "MEMBER")]
    [HttpPost("class-sessions/{sessionId:long}/bookings")]
    public async Task<IActionResult> Book(long sessionId, CancellationToken ct)
    {
        var result = await service.BookSessionAsync(CurrentUserId, sessionId, ct);
        return result.Booked
            ? StatusCode(StatusCodes.Status201Created, result)
            : StatusCode(StatusCodes.Status202Accepted, result);
    }

    [Authorize(Roles = "RECEPTIONIST")]
    [HttpPost("class-sessions/{sessionId:long}/members/{memberId:long}/bookings")]
    public async Task<IActionResult> RegisterMember(long sessionId, long memberId, CancellationToken ct)
    {
        var result = await service.BookSessionForMemberAsync(CurrentUserId, memberId, sessionId, ct);
        return result.Booked
            ? StatusCode(StatusCodes.Status201Created, result)
            : StatusCode(StatusCodes.Status202Accepted, result);
    }

    [Authorize(Roles = "MEMBER")]
    [HttpDelete("session-bookings/{bookingId:long}")]
    public async Task<IActionResult> Cancel(long bookingId, CancellationToken ct) =>
        Ok(await service.CancelBookingAsync(CurrentUserId, bookingId, ct));
}

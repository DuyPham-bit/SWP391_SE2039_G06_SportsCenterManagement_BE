using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.API.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

/// <summary>UC-43 Book Class and UC-44 Cancel Class Booking.</summary>
[Authorize(Roles = "MEMBER")]
public sealed class SessionBookingsController(ISessionBookingService service) : FlowControllerBase, IAsyncActionFilter
{
    [HttpGet("members/me/session-bookings")]
    [CenterCapability("book_class")]
    public async Task<IActionResult> GetMine(CancellationToken ct) =>
        Ok(await service.ListMyBookingsAsync(CurrentUserId, ct));

    /// <summary>201 when booked; 202 when the session was full and the member joined the waitlist.</summary>
    [HttpPost("class-sessions/{sessionId:long}/bookings")]
    [CenterCapability("book_class")]
    public async Task<IActionResult> Book(long sessionId, CancellationToken ct)
    {
        var result = await service.BookSessionAsync(CurrentUserId, sessionId, ct);
        return result.Booked
            ? StatusCode(StatusCodes.Status201Created, result)
            : StatusCode(StatusCodes.Status202Accepted, result);
    }

    [HttpDelete("session-bookings/{bookingId:long}")]
    [CenterCapability("cancel_booking")]
    public async Task<IActionResult> Cancel(long bookingId, CancellationToken ct) =>
        Ok(await service.CancelBookingAsync(CurrentUserId, bookingId, ct));

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        // Only tagged business failures are mapped here; unexpected errors remain available to the middleware.
        if (executed.ExceptionHandled || executed.Exception is not InvalidOperationException exception ||
            exception.Data["StatusCode"] is not int statusCode) return;

        executed.Result = new ObjectResult(new
        {
            code = exception.Data["Code"] as string,
            message = exception.Message,
            detail = exception.Message,
            proposedHandling = exception.Data["ProposedHandling"] as string,
            isDraftPolicy = exception.Data["IsDraftPolicy"] is true
        }) { StatusCode = statusCode };
        executed.ExceptionHandled = true;
    }
}

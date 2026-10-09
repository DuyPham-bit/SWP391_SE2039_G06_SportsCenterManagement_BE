using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.Checkins;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
[RequirePermission(PermissionCodes.MemberCenterRead)]
public sealed class CheckinsController(ICheckinService checkinService) : FlowControllerBase
{
    [HttpPost("centers/{centerId:long}/checkins")]
    public async Task<ActionResult<CheckinResponse>> CheckIn(
        long centerId,
        [FromBody] CheckinRequest request,
        CancellationToken cancellationToken)
    {
        var result = await checkinService.CheckInMemberAsync(CurrentUserId, centerId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("centers/{centerId:long}/checkins/today")]
    public async Task<ActionResult<IReadOnlyList<CheckinResponse>>> GetTodayCheckins(
        long centerId,
        CancellationToken cancellationToken)
    {
        var result = await checkinService.GetTodayCheckinsAsync(CurrentUserId, centerId, cancellationToken);
        return Ok(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.API.Filters;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers.Catalog;

[Route("api/campus-map")]
[FlowExceptionFilter]
public sealed class CampusMapController(CampusMapService campusMap, CenterAccessGuard centerAccess) : FlowControllerBase
{
    /// <summary>Public campus map: zones, rooms/courts, features and live status.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CampusMapDto>> Get([FromQuery] long? centerId, CancellationToken ct) =>
        Ok(await campusMap.GetAsync(centerId, ct));

    [HttpPut("zones/{zoneId:long}")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<ActionResult<CampusZoneDto>> UpdateZone(long zoneId, UpdateCampusZoneRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(centerAccess, User, await campusMap.GetZoneCenterIdAsync(zoneId, ct), ct);
        return Ok(await campusMap.UpdateZoneAsync(zoneId, request, CurrentUserId, ct));
    }
}

using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.Services.Features.CoreFlows;

namespace SportsCenterManagement.API.Controllers;

public sealed record MembershipPackageResponse(
    long Id,
    long CenterId,
    string Name,
    string? Description,
    int DurationDays,
    decimal Price,
    int? MaxClasses,
    string? AccessType);

[ApiController]
[Route("api/centers/{centerId:long}/membership-packages")]
public sealed class MembershipPackagesController(CoreFlowService coreFlowService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPackageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPackageResponse>>> GetActive(
        long centerId,
        CancellationToken cancellationToken)
    {
        var packages = await coreFlowService.GetActivePackagesAsync(centerId, cancellationToken);
        return Ok(packages.Select(package => new MembershipPackageResponse(
            package.Id,
            package.CenterId,
            package.Name,
            package.Description,
            package.DurationDays,
            package.Price,
            package.MaxClasses,
            package.AccessType)).ToList());
    }
}

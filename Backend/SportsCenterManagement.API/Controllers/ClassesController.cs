using Microsoft.AspNetCore.Mvc;
using ClassesResponses = SportsCenterManagement.BLL.DTOs.Classes.Responses;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/centers/{centerId:long}/classes")]
public sealed class ClassesController(IClassService classService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClassesResponses.ClassCatalogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassesResponses.ClassCatalogResponse>>> GetPublished(
        long centerId,
        CancellationToken cancellationToken)
    {
        var classes = await classService.GetPublishedClassesAsync(centerId, cancellationToken);
        return Ok(classes);
    }
}

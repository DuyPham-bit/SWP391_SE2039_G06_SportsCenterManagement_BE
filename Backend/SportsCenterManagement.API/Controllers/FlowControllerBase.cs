using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api")]
public abstract class FlowControllerBase : ControllerBase
{
    /// <summary>Identity always comes from the validated token, never from request data.</summary>
    protected long CurrentUserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return long.TryParse(claim, out var id) ? id : 0;
        }
    }
}

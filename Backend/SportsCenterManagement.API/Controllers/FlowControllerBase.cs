using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportsCenterManagement.BLL.Exceptions;

namespace SportsCenterManagement.API.Controllers;

/// <summary>Maps <see cref="FlowException"/> to its HTTP status with a <c>{ message }</c> body.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FlowExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not FlowException ex) return;
        context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = ex.StatusCode };
        context.ExceptionHandled = true;
    }
}

[ApiController]
[FlowExceptionFilter]
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SportsCenterManagement.BLL.Exceptions;

namespace SportsCenterManagement.API.Filters;

public sealed class FlowExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not FlowException error) return;
        context.Result = new ObjectResult(new
        {
            status = error.StatusCode,
            message = error.Message,
            detail = error.Message,
            traceId = context.HttpContext.TraceIdentifier
        }) { StatusCode = error.StatusCode };
        context.ExceptionHandled = true;
    }
}

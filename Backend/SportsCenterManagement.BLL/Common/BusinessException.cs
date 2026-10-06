using System.Net;

namespace SportsCenterManagement.BLL.Common;

public sealed class BusinessException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public static BusinessException BadRequest(string message) => new(HttpStatusCode.BadRequest, message);
    public static BusinessException Forbidden(string message) => new(HttpStatusCode.Forbidden, message);
    public static BusinessException NotFound(string message) => new(HttpStatusCode.NotFound, message);
    public static BusinessException Conflict(string message) => new(HttpStatusCode.Conflict, message);
}

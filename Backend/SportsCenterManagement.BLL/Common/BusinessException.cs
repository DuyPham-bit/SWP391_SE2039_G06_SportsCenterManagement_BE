using System.Net;

namespace SportsCenterManagement.BLL.Common;

public sealed class BusinessException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? Code { get; }
    public string? ProposedHandling { get; }
    public bool IsDraftPolicy { get; }

    public BusinessException(
        HttpStatusCode statusCode,
        string message,
        string? code = null,
        string? proposedHandling = null,
        bool isDraftPolicy = false) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        ProposedHandling = proposedHandling;
        IsDraftPolicy = isDraftPolicy;
    }

    public static BusinessException BadRequest(string message, string? code = null, string? proposedHandling = null) =>
        new(HttpStatusCode.BadRequest, message, code, proposedHandling);

    public static BusinessException Forbidden(string message, string? code = null, string? proposedHandling = null) =>
        new(HttpStatusCode.Forbidden, message, code, proposedHandling);

    public static BusinessException NotFound(string message, string? code = null, string? proposedHandling = null) =>
        new(HttpStatusCode.NotFound, message, code, proposedHandling);

    public static BusinessException Conflict(string message, string? code = null, string? proposedHandling = null) =>
        new(HttpStatusCode.Conflict, message, code, proposedHandling);
}

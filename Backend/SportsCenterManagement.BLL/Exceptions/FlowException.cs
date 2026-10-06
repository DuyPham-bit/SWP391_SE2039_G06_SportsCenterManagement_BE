namespace SportsCenterManagement.BLL.Exceptions;

/// <summary>
/// Business-rule failure carrying the HTTP status the presentation layer should answer with.
/// Derives from <see cref="InvalidOperationException"/> so existing catch blocks keep working (-> 400).
/// </summary>
public sealed class FlowException(int statusCode, string message) : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;

    public static FlowException BadRequest(string message) => new(400, message);
    public static FlowException Forbidden(string message) => new(403, message);
    public static FlowException NotFound(string message) => new(404, message);
    public static FlowException Conflict(string message) => new(409, message);
}

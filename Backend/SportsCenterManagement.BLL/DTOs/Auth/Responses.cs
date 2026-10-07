namespace SportsCenterManagement.BLL.DTOs.Auth;

public static class Responses
{
    public sealed record AuthenticatedUser(long UserId, string Username, string Role, long? CenterId, string Email = "");

    public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, string Username, string Role);
}

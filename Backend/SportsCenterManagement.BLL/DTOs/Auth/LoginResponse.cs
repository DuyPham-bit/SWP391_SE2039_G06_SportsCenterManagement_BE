namespace SportsCenterManagement.BLL.DTOs.Auth;

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, string Username, string Role);

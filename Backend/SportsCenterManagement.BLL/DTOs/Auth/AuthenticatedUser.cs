namespace SportsCenterManagement.BLL.DTOs.Auth;

public sealed record AuthenticatedUser(long UserId, string Username, string Role, long? CenterId);

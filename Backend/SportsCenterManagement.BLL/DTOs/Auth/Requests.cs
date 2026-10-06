using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Auth;

public static class Requests
{
    public sealed record LoginRequest(
        [property: Required, StringLength(150)] string Login,
        [property: Required, StringLength(150)] string Password);

    public sealed record RegisterRequest(
        [property: Required, StringLength(100, MinimumLength = 3)] string Username,
        [property: Required, StringLength(150)] string Email,
        [property: Required, StringLength(150, MinimumLength = 12)] string Password,
        [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
        [property: StringLength(20)] string? Phone,
        DateOnly? DateOfBirth,
        [property: Range(1, long.MaxValue)] long? CenterId = null);
}

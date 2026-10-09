using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Auth
{
    public static class Requests
    {
        public sealed record LoginRequest(
            [param: Required, StringLength(150)] string Login,
            [param: Required, StringLength(150)] string Password);

        public sealed record RegisterRequest(
            [param: Required, StringLength(100, MinimumLength = 3)] string Username,
            [param: Required, StringLength(150)] string Email,
            [param: Required, StringLength(128, MinimumLength = 6)] string Password,
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [param: StringLength(20)] string? Phone,
            DateOnly? DateOfBirth,
            [param: Range(1, long.MaxValue)] long? CenterId = null);
    }
}

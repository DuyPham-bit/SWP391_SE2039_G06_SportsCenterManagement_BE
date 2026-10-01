using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Auth;

public sealed record LoginRequest(
    [property: Required, StringLength(150)] string Login,
    [property: Required, StringLength(150)] string Password);

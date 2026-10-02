using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Auth;

/// <summary>
/// Yêu cầu đăng nhập tài khoản hệ thống.
/// </summary>
/// <param name="Login">Tên đăng nhập hoặc địa chỉ email.</param>
/// <param name="Password">Mật khẩu tài khoản.</param>
public sealed record LoginRequest(
    [property: Required, StringLength(150)] string Login,
    [property: Required, StringLength(150)] string Password);

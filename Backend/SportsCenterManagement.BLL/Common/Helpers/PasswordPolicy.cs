using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.Common.Helpers;

public static class PasswordPolicy
{
    public static string? GetValidationError(string password, string? email = null, string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6 || password.Length > 128)
            return "Mật khẩu phải có từ 6 đến 128 ký tự.";
        if (password.Any(char.IsWhiteSpace))
            return "Mật khẩu không được chứa khoảng trắng.";
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
            return "Mật khẩu phải gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";
        if (!string.IsNullOrWhiteSpace(email) && password.Contains(email, StringComparison.OrdinalIgnoreCase))
            return "Mật khẩu không được chứa email đăng nhập.";
        if (!string.IsNullOrWhiteSpace(phone) && password.Contains(phone, StringComparison.Ordinal))
            return "Mật khẩu không được chứa số điện thoại.";

        return null;
    }

    public static void ValidateOrThrow(string password, string? email = null, string? phone = null)
    {
        var error = GetValidationError(password, email, phone);
        if (error is not null)
            throw new ValidationException(error);
    }
}

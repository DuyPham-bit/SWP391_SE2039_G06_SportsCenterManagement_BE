using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Staff
{
    public static class Requests
    {
        public sealed record CreateCenterStaffRequest(
            [param: Required, StringLength(100, MinimumLength = 3)] string Username,
            [param: Required, EmailAddress, StringLength(150)] string Email,
            [param: Required, StringLength(128, MinimumLength = 6)] string Password,
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [param: Required, RegularExpression("^(Coach|Receptionist|Manager)$")] string RoleName,
            [param: StringLength(20)] string? Phone,
            DateOnly? HireDate,
            [param: StringLength(255)] string? Specialization,
            [param: StringLength(500)] string? Certification,
            [param: Range(0, 80)] int? ExperienceYears,
            [param: StringLength(1000)] string? Bio);

        public sealed record UpdateCenterStaffRequest(
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [param: EmailAddress, StringLength(150)] string? Email,
            [param: StringLength(20)] string? Phone,
            [param: Required, RegularExpression("^(Active|Disabled)$")] string Status,
            DateOnly? HireDate,
            [param: StringLength(255)] string? Specialization,
            [param: StringLength(500)] string? Certification,
            [param: Range(0, 80)] int? ExperienceYears,
            [param: StringLength(1000)] string? Bio);
    }
}

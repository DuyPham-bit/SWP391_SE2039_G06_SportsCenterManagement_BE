using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Staff
{
    public static class Requests
    {
        public sealed record CreateCenterStaffRequest(
            [property: Required, StringLength(100, MinimumLength = 3)] string Username,
            [property: Required, EmailAddress, StringLength(150)] string Email,
            [property: Required, StringLength(150, MinimumLength = 12)] string Password,
            [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [property: Required, RegularExpression("^(Coach|Receptionist|Manager)$")] string RoleName,
            [property: StringLength(20)] string? Phone,
            DateOnly? HireDate,
            [property: StringLength(255)] string? Specialization,
            [property: StringLength(500)] string? Certification,
            [property: Range(0, 80)] int? ExperienceYears,
            [property: StringLength(1000)] string? Bio);

        public sealed record UpdateCenterStaffRequest(
            [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [property: EmailAddress, StringLength(150)] string? Email,
            [property: StringLength(20)] string? Phone,
            [property: Required, RegularExpression("^(Active|Disabled)$")] string Status,
            DateOnly? HireDate,
            [property: StringLength(255)] string? Specialization,
            [property: StringLength(500)] string? Certification,
            [property: Range(0, 80)] int? ExperienceYears,
            [property: StringLength(1000)] string? Bio);
    }
}

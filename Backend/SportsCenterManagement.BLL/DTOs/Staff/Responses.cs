namespace SportsCenterManagement.BLL.DTOs.Staff;

public static class Responses
{
    public sealed record CenterStaffResponse(
        long UserId,
        long CenterId,
        string StaffCode,
        string? CoachCode,
        string Username,
        string Email,
        string? Phone,
        string RoleName,
        string FullName,
        string Status,
        DateOnly? HireDate,
        string? Specialization,
        string? Certification,
        int? ExperienceYears,
        string? Bio);
}

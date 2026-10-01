using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Members;

public sealed record UpdateMemberProfileRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
    [property: StringLength(20)] string? Phone,
    DateOnly? DateOfBirth,
    [property: StringLength(20)] string? Gender,
    [property: StringLength(255)] string? Address);

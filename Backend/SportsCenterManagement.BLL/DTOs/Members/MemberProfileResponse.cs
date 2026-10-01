namespace SportsCenterManagement.BLL.DTOs.Members;

public sealed record MemberProfileResponse(
    long MemberId,
    long UserId,
    string MemberCode,
    string Username,
    string Email,
    string? Phone,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    DateTime CreatedAt);

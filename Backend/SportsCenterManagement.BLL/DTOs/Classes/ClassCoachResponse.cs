namespace SportsCenterManagement.BLL.DTOs.Classes;

public sealed record ClassCoachResponse(
    long ClassId,
    long CoachId,
    string CoachName,
    string CoachCode,
    bool IsPrimary,
    DateOnly? AssignedDate,
    string? Specialization);

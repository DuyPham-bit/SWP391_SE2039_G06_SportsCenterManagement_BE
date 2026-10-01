using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Classes;

public sealed record AssignCoachRequest
{
    [Required]
    public required long CoachId { get; init; }

    public bool IsPrimary { get; init; } = true;
}

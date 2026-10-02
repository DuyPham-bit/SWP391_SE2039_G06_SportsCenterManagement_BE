using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Classes;

public sealed record AssignCoachRequest
{
    [Required]
<<<<<<< Updated upstream
    public required long CoachId { get; init; }

=======
    public long CoachId { get; init; }
>>>>>>> Stashed changes
    public bool IsPrimary { get; init; } = true;
}

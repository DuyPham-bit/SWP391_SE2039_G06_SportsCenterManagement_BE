using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Classes;

/// <summary>
/// Yêu cầu phân công Huấn luyện viên (Coach) vào một lớp học.
/// </summary>
public sealed record AssignCoachRequest
{
    /// <summary>
    /// ID định danh của Huấn luyện viên được phân công.
    /// </summary>
    [Required]
    public required long CoachId { get; init; }

    /// <summary>
    /// Đánh dấu là Huấn luyện viên chính của lớp hay trợ giảng (mặc định: true).
    /// </summary>
    public bool IsPrimary { get; init; } = true;
}

using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Support;

public sealed record CreateSupportRequest(
    [Range(1, long.MaxValue)] long MemberId,
    [Required, StringLength(200)] string Subject,
    [Required, StringLength(5000)] string Description,
    [RegularExpression("^(Low|Normal|High|Urgent)$")] string? Priority = null);

public sealed record UpdateSupportRequest(
    [Required, RegularExpression("^(Open|InProgress|Resolved)$")] string Status,
    [StringLength(5000)] string? Note,
    [RegularExpression("^(Low|Normal|High|Urgent)$")] string? Priority = null);

public sealed record SubmitSupportRequest(
    [Required, StringLength(200)] string Subject,
    [Required, StringLength(5000)] string Description,
    [RegularExpression("^(Low|Normal|High|Urgent)$")] string? Priority = null);

public sealed record ReplyToSupportRequest([Required, StringLength(5000)] string Message);


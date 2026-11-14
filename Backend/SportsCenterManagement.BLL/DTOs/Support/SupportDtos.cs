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

public sealed record SupportSummary(long Id, long? MemberId, string? MemberName, string? MemberCode,
    string Subject, string Status, DateTime CreatedAt, DateTime? ResolvedAt,
    long RequesterId, string RequesterName, string RequesterRole, long CenterId, string Priority = "Normal");
/// <summary>SenderName is the person's full name (staff/member/coach profile), falling back to the username.</summary>
public sealed record SupportMessage(long Id, string SenderName, string Message, DateTime CreatedAt,
    long SenderId = 0, string SenderRole = "");
public sealed record SupportDetails(SupportSummary Request, string Description, IReadOnlyList<SupportMessage> Messages);
/// <summary>Counts over the caller's whole scope, independent of the status/search filter and paging.</summary>
public sealed record SupportStatusCounts(int Total, int Open, int InProgress, int Resolved);

public sealed record SupportPage(IReadOnlyList<SupportSummary> Items, int TotalCount, int Page, int PageSize,
    SupportStatusCounts? StatusCounts = null);

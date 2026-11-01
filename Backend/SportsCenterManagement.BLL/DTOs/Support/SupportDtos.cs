using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Support;

public sealed record CreateSupportRequest(
    [Range(1, long.MaxValue)] long MemberId,
    [Required, StringLength(200)] string Subject,
    [Required, StringLength(5000)] string Description);

public sealed record UpdateSupportRequest(
    [Required, RegularExpression("^(Open|InProgress|Resolved)$")] string Status,
    [StringLength(5000)] string? Note);

public sealed record SubmitSupportRequest(
    [Required, StringLength(200)] string Subject,
    [Required, StringLength(5000)] string Description);

public sealed record ReplyToSupportRequest([Required, StringLength(5000)] string Message);

public sealed record SupportSummary(long Id, long? MemberId, string? MemberName, string? MemberCode,
    string Subject, string Status, DateTime CreatedAt, DateTime? ResolvedAt,
    long RequesterId, string RequesterName, string RequesterRole, long CenterId);
public sealed record SupportMessage(long Id, string SenderName, string Message, DateTime CreatedAt);
public sealed record SupportDetails(SupportSummary Request, string Description, IReadOnlyList<SupportMessage> Messages);
public sealed record SupportPage(IReadOnlyList<SupportSummary> Items, int TotalCount, int Page, int PageSize);

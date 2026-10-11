using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Support;

public sealed record CreateSupportRequest(
    [property: Range(1, long.MaxValue)] long MemberId,
    [property: Required, StringLength(200)] string Subject,
    [property: Required, StringLength(5000)] string Description,
    [property: Required, RegularExpression("^(Low|Normal|High)$")] string Priority);

public sealed record UpdateSupportRequest(
    [property: Required, RegularExpression("^(Open|InProgress|Resolved)$")] string Status,
    [property: StringLength(5000)] string? Note);

public sealed record SupportSummary(long Id, long MemberId, string MemberName, string MemberCode,
    string Subject, string Priority, string Status, DateTime CreatedAt, DateTime? ResolvedAt);
public sealed record SupportMessage(long Id, string SenderName, string Message, DateTime CreatedAt);
public sealed record SupportDetails(SupportSummary Request, string Description, IReadOnlyList<SupportMessage> Messages);
public sealed record SupportPage(IReadOnlyList<SupportSummary> Items, int TotalCount, int Page, int PageSize);

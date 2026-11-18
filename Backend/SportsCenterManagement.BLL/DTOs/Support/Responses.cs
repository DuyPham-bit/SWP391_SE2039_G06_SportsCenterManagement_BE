using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Support;



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


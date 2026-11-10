using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;

namespace SportsCenterManagement.BLL.DTOs.Members.Activation;

/// <summary>Counter quick-create: the phone number becomes the initial (must-change) password.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QuickCreateMemberRequest(
    [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
    [param: Required, StringLength(20)] string PhoneNumber,
    [param: Required, StringLength(150)] string Email,
    [param: StringLength(20)] string? Gender = null,
    DateOnly? DateOfBirth = null,
    [param: StringLength(255)] string? Address = null);

public sealed record ActivationPreviewRequest([param: Required, StringLength(128)] string Token);

public sealed record VerifyAndChangePasswordRequest(
    [param: Required, StringLength(128)] string Token,
    [param: Required, StringLength(128)] string NewPassword,
    [param: Required, StringLength(128)] string ConfirmPassword);

public sealed record InitialPasswordChangeRequest(
    [param: Required, StringLength(128)] string CurrentPassword,
    [param: Required, StringLength(128)] string NewPassword,
    [param: Required, StringLength(128)] string ConfirmPassword);

/// <summary>
/// Result of issuing an activation link. <see cref="RawToken"/> must only be placed in the email
/// and never returned to the receptionist's client.
/// </summary>
public sealed record ActivationIssue(
    MembersResponses.MemberProfileResponse Member,
    long UserId,
    string RawToken,
    DateTime ExpiresAt);

public sealed record ActivationPreviewResponse(string FullName, string Email, string? Phone, DateTime ExpiresAt);

/// <summary>Response to the receptionist after quick-create or resend.</summary>
public sealed record ActivationDispatchResponse(
    MembersResponses.MemberProfileResponse Member,
    bool EmailSent,
    string Message,
    int ResendCooldownSeconds);

public sealed record MemberVerificationStatus(long MemberId, string FullName, string MemberCode, bool IsEmailVerified);

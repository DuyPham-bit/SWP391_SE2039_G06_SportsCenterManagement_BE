using SportsCenterManagement.BLL.DTOs.Members.Activation;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMemberActivationService
{
    /// <summary>Creates a Member at the actor's center with the phone number as a must-change password.</summary>
    Task<ActivationIssue> QuickCreateAsync(long actorUserId, QuickCreateMemberRequest request, CancellationToken cancellationToken = default);

    /// <summary>Invalidates outstanding links and issues a new one (60 s cooldown).</summary>
    Task<ActivationIssue> ReissueAsync(long actorUserId, long memberId, CancellationToken cancellationToken = default);

    /// <summary>Marks an issued token unusable, e.g. when the email could not be delivered.</summary>
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Email-verification state of members at the actor's center (for live receptionist updates).</summary>
    Task<IReadOnlyList<MemberVerificationStatus>> GetVerificationStatusAsync(long actorUserId, IReadOnlyCollection<long> memberIds, CancellationToken cancellationToken = default);

    Task<ActivationPreviewResponse> PreviewAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Verifies the email and replaces the default password in one step.</summary>
    Task CompleteAsync(VerifyAndChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>First-login fallback: replaces the default password for a signed-in must-change account.</summary>
    Task<User> ChangeInitialPasswordAsync(long userId, InitialPasswordChangeRequest request, CancellationToken cancellationToken = default);
}

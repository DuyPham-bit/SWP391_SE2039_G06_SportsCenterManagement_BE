using AuthRequests = SportsCenterManagement.BLL.DTOs.Auth.Requests;
using AuthResponses = SportsCenterManagement.BLL.DTOs.Auth.Responses;
using MembersRequests = SportsCenterManagement.BLL.DTOs.Members.Requests;
using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMemberService
{
    Task<MembersResponses.MemberProfileResponse> GetMeAsync(long userId, CancellationToken cancellationToken = default);
    Task<MembersResponses.MemberProfileResponse> UpdateMeAsync(long userId, MembersRequests.UpdateMemberProfileRequest request, CancellationToken cancellationToken = default);
    Task<MembersResponses.MemberProfileResponse> UpdateAtCenterAsync(long actorUserId, long centerId, long memberId, MembersRequests.UpdateMemberAtCenterRequest request, CancellationToken cancellationToken = default);
    Task<MembersResponses.MemberProfileResponse> SetMemberStatusAsync(long actorUserId, long centerId, long memberId, string status, CancellationToken cancellationToken = default);
    Task<MembersResponses.PagedResponse<MembersResponses.MemberProfileResponse>> SearchAtCenterAsync(
        long actorUserId,
        long centerId,
        string query,
        int page,
        int pageSize,
        string sortBy,
        string sortDirection,
        CancellationToken cancellationToken = default);
    Task<MembersResponses.MemberProfileResponse> CreateAtCenterAsync(long actorUserId, long centerId, AuthRequests.RegisterRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MembersResponses.MemberSubscriptionResponse>> GetSubscriptionsAsync(long actorUserId, long memberId, CancellationToken cancellationToken = default);
    /// <summary>Counter staff of the member's center may change the member's photo; returns the member's user id.</summary>
    Task<long> GetMemberUserIdForStaffAsync(long actorUserId, long centerId, long memberId, CancellationToken cancellationToken = default);
    /// <summary>Stores the photo key (null removes it); returns the updated member and the replaced key to delete.</summary>
    Task<(MembersResponses.MemberProfileResponse Member, string? PreviousKey)> SetMemberAvatarAsync(
        long actorUserId, long centerId, long memberId, string? avatarKey, CancellationToken cancellationToken = default);
    Task EnsureCanBuyPackageAsync(long userId, long packageId, CancellationToken cancellationToken = default);
    Task EnsureCanProcessInvoiceAsync(long actorUserId, long invoiceId, CancellationToken cancellationToken = default);
    Task EnsureCanSellToMemberAsync(long actorUserId, long memberId, long packageId, CancellationToken cancellationToken = default);
}

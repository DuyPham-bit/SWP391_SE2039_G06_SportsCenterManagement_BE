using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMemberService
{
    Task<MemberProfileResponse> GetMeAsync(long userId, CancellationToken cancellationToken = default);
    Task<MemberProfileResponse> UpdateMeAsync(long userId, UpdateMemberProfileRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<MemberProfileResponse>> SearchAtCenterAsync(
        long actorUserId,
        long centerId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<MemberProfileResponse> CreateAtCenterAsync(long actorUserId, long centerId, RegisterRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberSubscriptionResponse>> GetSubscriptionsAsync(long actorUserId, long memberId, CancellationToken cancellationToken = default);
    Task EnsureCanBuyPackageAsync(long userId, long packageId, CancellationToken cancellationToken = default);
    Task EnsureCanProcessInvoiceAsync(long actorUserId, long invoiceId, CancellationToken cancellationToken = default);
    Task EnsureCanSellToMemberAsync(long actorUserId, long memberId, long packageId, CancellationToken cancellationToken = default);
}

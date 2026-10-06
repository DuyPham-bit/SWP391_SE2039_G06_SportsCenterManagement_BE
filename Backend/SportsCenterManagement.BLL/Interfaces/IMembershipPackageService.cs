using MembershipPackagesRequests = SportsCenterManagement.BLL.DTOs.MembershipPackages.Requests;
using MembershipPackagesResponses = SportsCenterManagement.BLL.DTOs.MembershipPackages.Responses;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMembershipPackageService
{
    Task<IReadOnlyList<MembershipPackagesResponses.MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    Task<MembershipPackagesResponses.MembershipPackageResponse> CreateAsync(
        long actorUserId,
        long centerId,
        MembershipPackagesRequests.CreateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<MembershipPackagesResponses.MembershipPackageResponse> UpdateAsync(
        long actorUserId,
        long packageId,
        MembershipPackagesRequests.UpdateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);
}

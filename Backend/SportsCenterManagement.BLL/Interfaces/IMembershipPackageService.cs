using SportsCenterManagement.BLL.DTOs.MembershipPackages;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMembershipPackageService
{
    Task<IReadOnlyList<MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    Task<MembershipPackageResponse> CreateAsync(
        long actorUserId,
        long centerId,
        CreateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<MembershipPackageResponse> UpdateAsync(
        long actorUserId,
        long packageId,
        UpdateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);
}

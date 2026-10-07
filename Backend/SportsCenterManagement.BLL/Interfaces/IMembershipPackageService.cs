using SportsCenterManagement.BLL.DTOs.MembershipPackages;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMembershipPackageService
{
    Task<IReadOnlyList<MembershipPackageResponse>> GetPackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    Task<MembershipPackageResponse> CreatePackageAsync(
        long actorUserId,
        long centerId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<MembershipPackageResponse> UpdatePackageAsync(
        long actorUserId,
        long centerId,
        long packageId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<MembershipPackageResponse> SetPackageStatusAsync(
        long actorUserId,
        long centerId,
        long packageId,
        string status,
        CancellationToken cancellationToken = default);

    Task<MembershipPackageResponse> CreateAsync(
        long actorUserId,
        long centerId,
        Requests.CreateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<MembershipPackageResponse> UpdateAsync(
        long actorUserId,
        long packageId,
        Requests.UpdateMembershipPackageRequest request,
        CancellationToken cancellationToken = default);
}

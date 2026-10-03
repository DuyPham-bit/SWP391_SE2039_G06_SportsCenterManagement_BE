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
        long centerId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default);
    Task<MembershipPackageResponse> UpdatePackageAsync(
        long centerId,
        long packageId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default);
    Task<MembershipPackageResponse> SetPackageStatusAsync(
        long centerId,
        long packageId,
        string status,
        CancellationToken cancellationToken = default);
}

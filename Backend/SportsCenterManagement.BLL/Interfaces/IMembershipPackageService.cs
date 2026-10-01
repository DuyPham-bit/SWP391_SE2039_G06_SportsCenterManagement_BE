using SportsCenterManagement.BLL.DTOs.MembershipPackages;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IMembershipPackageService
{
    Task<IReadOnlyList<MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default);
}

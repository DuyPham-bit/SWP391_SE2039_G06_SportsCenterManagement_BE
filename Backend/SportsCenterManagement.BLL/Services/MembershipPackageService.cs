using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.MembershipPackages;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class MembershipPackageService(IUnitOfWork unitOfWork) : IMembershipPackageService
{
    public async Task<IReadOnlyList<MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Repository<MembershipPackage>()
            .Find(package => package.CenterId == centerId && package.Status == "Active")
            .OrderBy(package => package.Price)
            .Select(package => new MembershipPackageResponse(
                package.Id,
                package.CenterId,
                package.Name,
                package.Description,
                package.DurationDays,
                package.Price,
                package.MaxClasses,
                package.AccessType,
                package.Status))
            .ToListAsync(cancellationToken);
    }
}


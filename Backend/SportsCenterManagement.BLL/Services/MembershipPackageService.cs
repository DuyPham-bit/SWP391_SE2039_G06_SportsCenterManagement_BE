using Microsoft.EntityFrameworkCore;
using System.Data;
using MembershipPackagesRequests = SportsCenterManagement.BLL.DTOs.MembershipPackages.Requests;
using MembershipPackagesResponses = SportsCenterManagement.BLL.DTOs.MembershipPackages.Responses;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class MembershipPackageService(IUnitOfWork unitOfWork) : IMembershipPackageService
{
    public async Task<IReadOnlyList<MembershipPackagesResponses.MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
    {
        if (centerId <= 0 || !await unitOfWork.Repository<Center>()
                .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken))
        {
            throw new KeyNotFoundException("Không tìm thấy trung tâm đang hoạt động.");
        }

        return await unitOfWork.Repository<MembershipPackage>()
            .Find(package => package.CenterId == centerId && package.Status == "Active")
            .OrderBy(package => package.Price)
            .Select(package => new MembershipPackagesResponses.MembershipPackageResponse(
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

    public async Task<MembershipPackagesResponses.MembershipPackageResponse> CreateAsync(
        long actorUserId,
        long centerId,
        MembershipPackagesRequests.CreateMembershipPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actorUserId, centerId, cancellationToken);
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var name = request.Name.Trim();
        if (await unitOfWork.Repository<MembershipPackage>().AnyAsync(
                package => package.CenterId == centerId && package.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Tên gói đã được dùng tại trung tâm này.");
        }

        var now = DateTime.UtcNow;
        var package = new MembershipPackage
        {
            CenterId = centerId,
            Name = name,
            Description = Normalize(request.Description),
            DurationDays = request.DurationDays,
            Price = request.Price,
            MaxClasses = request.MaxClasses,
            AccessType = Normalize(request.AccessType),
            Status = request.Status,
            CreatedAt = now
        };
        await unitOfWork.Repository<MembershipPackage>().AddAsync(package, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "membership_package.created",
            "MembershipPackage", package.Id,
            newValues: new { package.Name, package.Price, package.DurationDays, package.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(package);
    }

    public async Task<MembershipPackagesResponses.MembershipPackageResponse> UpdateAsync(
        long actorUserId,
        long packageId,
        MembershipPackagesRequests.UpdateMembershipPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var package = await unitOfWork.Repository<MembershipPackage>().GetByIdAsync(packageId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy gói tập.");
        await EnsureManagerAsync(actorUserId, package.CenterId, cancellationToken);
        var name = request.Name.Trim();
        if (await unitOfWork.Repository<MembershipPackage>().AnyAsync(
                item => item.Id != packageId && item.CenterId == package.CenterId
                        && item.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            throw new InvalidOperationException("Tên gói đã được dùng tại trung tâm này.");
        }

        var oldValues = new { package.Name, package.Price, package.DurationDays, package.Status };
        package.Name = name;
        package.Description = Normalize(request.Description);
        package.DurationDays = request.DurationDays;
        package.Price = request.Price;
        package.MaxClasses = request.MaxClasses;
        package.AccessType = Normalize(request.AccessType);
        package.Status = request.Status;
        package.UpdatedAt = DateTime.UtcNow;
        unitOfWork.Repository<MembershipPackage>().Update(package);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, package.CenterId, "membership_package.updated",
            "MembershipPackage", package.Id, oldValues,
            new { package.Name, package.Price, package.DurationDays, package.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(package);
    }

    private async Task EnsureManagerAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        if (!await unitOfWork.Repository<Center>()
                .AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken))
        {
            throw new KeyNotFoundException("Không tìm thấy trung tâm đang hoạt động.");
        }
        var user = await unitOfWork.Repository<User>().GetByIdAsync(actorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không có quyền thao tác.");
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(user.RoleId, cancellationToken);
        var assigned = await unitOfWork.Repository<StaffProfile>()
            .AnyAsync(profile => profile.UserId == actorUserId && profile.CenterId == centerId && profile.Status == "Active", cancellationToken);
        if (user.Status != "Active" || role?.Name != "Manager" || !assigned)
        {
            throw new UnauthorizedAccessException("Chỉ Manager được quản lý gói của trung tâm được gán.");
        }
    }

    private static MembershipPackagesResponses.MembershipPackageResponse Map(MembershipPackage package) => new(
        package.Id, package.CenterId, package.Name, package.Description, package.DurationDays,
        package.Price, package.MaxClasses, package.AccessType, package.Status);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.MembershipPackages;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class MembershipPackageService(IUnitOfWork unitOfWork) : IMembershipPackageService
{
    public Task<IReadOnlyList<MembershipPackageResponse>> GetPackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default) =>
        MapPackagesAsync(centerId, activeOnly: false, cancellationToken);

    public async Task<IReadOnlyList<MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
        => await MapPackagesAsync(centerId, activeOnly: true, cancellationToken);

    public async Task<MembershipPackageResponse> CreatePackageAsync(
        long centerId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCenterExistsAsync(centerId, cancellationToken);
        Validate(request);

        var now = DateTime.UtcNow;
        var package = new MembershipPackage
        {
            CenterId = centerId,
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            DurationDays = request.DurationDays,
            Price = request.Price,
            MaxClasses = request.MaxClasses,
            AccessType = Normalize(request.AccessType),
            AllowedSports = request.AllowedSports,
            Badge = Normalize(request.Badge),
            Features = JsonSerializer.Serialize(request.Features ?? []),
            Status = "Active",
            CreatedAt = now
        };

        await unitOfWork.Repository<MembershipPackage>().AddAsync(package, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(package);
    }

    public async Task<MembershipPackageResponse> CreateAsync(
        long actorUserId,
        long centerId,
        Requests.CreateMembershipPackageRequest request,
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
        return ToResponse(package);
    }

    public async Task<MembershipPackageResponse> UpdatePackageAsync(
        long centerId,
        long packageId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var package = await FindPackageAsync(centerId, packageId, cancellationToken);
        package.Name = request.Name.Trim();
        package.Description = Normalize(request.Description);
        package.DurationDays = request.DurationDays;
        package.Price = request.Price;
        package.MaxClasses = request.MaxClasses;
        package.AccessType = Normalize(request.AccessType);
        package.AllowedSports = request.AllowedSports;
        package.Badge = Normalize(request.Badge);
        package.Features = JsonSerializer.Serialize(request.Features ?? []);
        package.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(package);
    }

    public async Task<MembershipPackageResponse> UpdateAsync(
        long actorUserId,
        long packageId,
        Requests.UpdateMembershipPackageRequest request,
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
        return ToResponse(package);
    }

    public async Task<MembershipPackageResponse> SetPackageStatusAsync(
        long centerId,
        long packageId,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Status must be Active or Inactive.", nameof(status));
        }

        var package = await FindPackageAsync(centerId, packageId, cancellationToken);
        package.Status = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ? "Active" : "Inactive";
        package.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(package);
    }

    private async Task<IReadOnlyList<MembershipPackageResponse>> MapPackagesAsync(
        long centerId,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<MembershipPackage>().Find(package => package.CenterId == centerId);
        if (activeOnly)
        {
            query = query.Where(package => package.Status == "Active");
        }

        var packages = await query
            .OrderBy(package => package.Price)
            .ToListAsync(cancellationToken);
        return packages.Select(ToResponse).ToList();
    }

    private async Task<MembershipPackage> FindPackageAsync(long centerId, long packageId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<MembershipPackage>()
            .Find(package => package.Id == packageId && package.CenterId == centerId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new KeyNotFoundException("Membership package was not found for this center.");

    private async Task EnsureCenterExistsAsync(long centerId, CancellationToken cancellationToken)
    {
        if (!await unitOfWork.Repository<Center>().AnyAsync(center => center.Id == centerId, cancellationToken))
        {
            throw new KeyNotFoundException("Center was not found.");
        }
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

    private static void Validate(SaveMembershipPackageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Package name is required.");
        }
        if (request.DurationDays <= 0 || request.Price <= 0 || request.MaxClasses is <= 0 || request.AllowedSports <= 0)
        {
            throw new ArgumentException("Duration, price, and class limit must be positive.");
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MembershipPackageResponse ToResponse(MembershipPackage package) => new(
        package.Id,
        package.CenterId,
        package.Name,
        package.Description,
        package.DurationDays,
        package.Price,
        package.MaxClasses,
        package.AccessType,
        package.Status,
        package.AllowedSports,
        package.Badge,
        ParseFeatures(package.Features));

    private static IReadOnlyList<string> ParseFeatures(string? features)
    {
        if (string.IsNullOrWhiteSpace(features)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(features) ?? []; }
        catch (JsonException) { return []; }
    }
}


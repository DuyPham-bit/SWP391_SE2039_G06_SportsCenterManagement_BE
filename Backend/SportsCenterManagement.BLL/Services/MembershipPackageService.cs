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
    private const decimal MaxPackagePrice = 9_999_999_999m;

    public Task<IReadOnlyList<MembershipPackageResponse>> GetPackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default) =>
        MapPackagesAsync(centerId, activeOnly: false, cancellationToken);

    public async Task<IReadOnlyList<MembershipPackageResponse>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
        => await MapPackagesAsync(centerId, activeOnly: true, cancellationToken);

    public async Task<MembershipPackageResponse> CreatePackageAsync(
        long actorUserId,
        long centerId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCenterExistsAsync(centerId, cancellationToken);
        Validate(request);
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

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
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "membership_package.created",
            "MembershipPackage", package.Id,
            newValues: new { package.Name, package.Price, package.DurationDays, package.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        long actorUserId,
        long centerId,
        long packageId,
        SaveMembershipPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var package = await FindPackageAsync(centerId, packageId, cancellationToken);
        await EnsurePackageCanBeModifiedAsync(packageId, cancellationToken);
        var oldValues = new { package.Name, package.Price, package.DurationDays, package.Status };
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
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "membership_package.updated",
            "MembershipPackage", package.Id, oldValues,
            new { package.Name, package.Price, package.DurationDays, package.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        await EnsurePackageCanBeModifiedAsync(packageId, cancellationToken);
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
        long actorUserId,
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

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var package = await FindPackageAsync(centerId, packageId, cancellationToken);
        var previousStatus = package.Status;
        var newStatus = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ? "Active" : "Inactive";
        if (string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            var currentSubCount = await unitOfWork.Context.MemberSubscriptions
                .CountAsync(s => s.PackageId == package.Id, cancellationToken);
            var currentHasInvoices = await unitOfWork.Context.InvoiceItems
                .AnyAsync(i => i.PackageId == package.Id, cancellationToken);
            var currentHasOrders = currentSubCount > 0 || currentHasInvoices;
            return ToResponse(package, currentSubCount, currentHasOrders, !currentHasOrders);
        }
        package.Status = newStatus;
        package.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, centerId, "membership_package.status_updated",
            "MembershipPackage", package.Id,
            new { Status = previousStatus }, new { Status = package.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var subCount = await unitOfWork.Context.MemberSubscriptions
            .CountAsync(s => s.PackageId == package.Id, cancellationToken);
        var hasInvoices = await unitOfWork.Context.InvoiceItems
            .AnyAsync(i => i.PackageId == package.Id, cancellationToken);
        var hasOrders = subCount > 0 || hasInvoices;
        return ToResponse(package, subCount, hasOrders, !hasOrders);
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
            .OrderBy(package => package.Status == "Inactive" ? 1 : 0)
            .ThenBy(package => package.Price)
            .ToListAsync(cancellationToken);

        if (packages.Count == 0) return [];

        var packageIds = packages.Select(package => package.Id).ToList();

        var subscriptionCounts = await unitOfWork.Context.MemberSubscriptions
            .Where(s => packageIds.Contains(s.PackageId))
            .GroupBy(s => s.PackageId)
            .Select(g => new { PackageId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PackageId, x => x.Count, cancellationToken);

        var invoicedPackageIds = (await unitOfWork.Context.InvoiceItems
            .Where(i => i.PackageId.HasValue && packageIds.Contains(i.PackageId.Value))
            .Select(i => i.PackageId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return packages.Select(package =>
        {
            subscriptionCounts.TryGetValue(package.Id, out var subCount);
            var hasOrders = subCount > 0 || invoicedPackageIds.Contains(package.Id);
            var isEditable = !hasOrders;
            return ToResponse(package, subCount, hasOrders, isEditable);
        }).ToList();
    }

    private async Task EnsurePackageCanBeModifiedAsync(long packageId, CancellationToken cancellationToken)
    {
        var hasSubscriptions = await unitOfWork.Context.MemberSubscriptions
            .AnyAsync(s => s.PackageId == packageId, cancellationToken);
        var hasInvoices = await unitOfWork.Context.InvoiceItems
            .AnyAsync(i => i.PackageId == packageId, cancellationToken);

        if (hasSubscriptions || hasInvoices)
        {
            var ex = new InvalidOperationException("Gói tập đã kích hoạt và có người đặt/sử dụng, không thể chỉnh sửa thông tin gói.");
            ex.Data["Code"] = "PKG_HAS_ORDERS";
            ex.Data["StatusCode"] = 409;
            ex.Data["ProposedHandling"] = "Để thay đổi chính sách hoặc biểu phí, vui lòng khóa bán gói hiện tại và tạo một gói mới.";
            throw ex;
        }
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
        if (request.DurationDays <= 0 || request.MaxClasses is <= 0 || request.AllowedSports <= 0)
        {
            throw new ArgumentException("Thời hạn, số lớp và số môn phải lớn hơn 0.");
        }
        if (request.Price <= 0 || request.Price > MaxPackagePrice)
        {
            throw new ArgumentException($"Giá gói tập phải lớn hơn 0 và không vượt quá {MaxPackagePrice:N0} VNĐ.");
        }
        if (decimal.Round(request.Price, 2) != request.Price)
        {
            throw new ArgumentException("Giá gói tập chỉ được có tối đa 2 chữ số thập phân.");
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MembershipPackageResponse ToResponse(
        MembershipPackage package,
        int subscriberCount = 0,
        bool hasOrders = false,
        bool isEditable = true) => new(
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
        ParseFeatures(package.Features),
        subscriberCount,
        hasOrders,
        isEditable);

    private static IReadOnlyList<string> ParseFeatures(string? features)
    {
        if (string.IsNullOrWhiteSpace(features)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(features) ?? []; }
        catch (JsonException) { return []; }
    }
}


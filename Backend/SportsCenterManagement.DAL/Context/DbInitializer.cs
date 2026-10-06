using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context)
    {
        await context.Database.MigrateAsync();

        if (!await context.Centers.AnyAsync())
        {
            await context.Centers.AddAsync(new Center
            {
                Name = "Sports Center Quận 1",
                Address = "123 Nguyễn Thị Minh Khai, Quận 1, TP.HCM",
                Phone = "0353716249",
                Email = "center.q1@sportscenter.vn",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var center = await context.Centers.OrderBy(item => item.Id).FirstAsync();
        if (!await context.MembershipPackages.AnyAsync())
        {
            await context.MembershipPackages.AddRangeAsync(
                new MembershipPackage
                {
                    CenterId = center.Id,
                    Name = "Gói Gym & Yoga 1 Tháng",
                    Description = "Tập luyện không giới hạn trong 30 ngày",
                    DurationDays = 30,
                    Price = 500000,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new MembershipPackage
                {
                    CenterId = center.Id,
                    Name = "Gói VIP Premium 3 Tháng",
                    Description = "Toàn quyền sử dụng dịch vụ trong 90 ngày",
                    DurationDays = 90,
                    Price = 1200000,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();
        }

        foreach (var roleName in new[]
                 {
                     RoleNames.Member, RoleNames.Coach, RoleNames.Manager,
                     RoleNames.Receptionist, RoleNames.SystemAdmin
                 })
        {
            if (await context.Roles.AnyAsync(role => role.Name == roleName))
            {
                continue;
            }

            await context.Roles.AddAsync(new Role
            {
                Name = roleName,
                Description = roleName switch
                {
                    RoleNames.Member => "Khách hàng hội viên",
                    RoleNames.SystemAdmin => "Quản trị viên toàn hệ thống",
                    _ => "Nhân sự trung tâm"
                },
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var permissionDefinitions = new (string Code, string Name, string Description)[]
        {
            (PermissionCodes.MemberSelfRead, "View own profile", "View the authenticated member's own profile."),
            (PermissionCodes.MemberSelfUpdate, "Update own profile", "Update the authenticated member's own profile."),
            (PermissionCodes.MemberCenterRead, "View center members", "Search and view members assigned to a center."),
            (PermissionCodes.MemberCenterCreate, "Create center members", "Register members at a center."),
            (PermissionCodes.MemberCenterUpdate, "Update center members", "Update member profile details at a center."),
            (PermissionCodes.MemberCenterManageStatus, "Manage member status", "Activate or disable a member account."),
            (PermissionCodes.StaffCenterRead, "View center staff", "View coach and staff accounts assigned to a center."),
            (PermissionCodes.StaffCenterCreate, "Create center staff", "Create coach and receptionist accounts at a center."),
            (PermissionCodes.StaffCenterUpdate, "Update center staff", "Update center staff profiles and statuses."),
            (PermissionCodes.MembershipPackagesManage, "Manage membership packages", "Create and update membership packages."),
            (PermissionCodes.SubscriptionSelfCreate, "Buy own subscription", "Create a subscription for the authenticated member."),
            (PermissionCodes.SubscriptionCenterCreate, "Sell subscriptions", "Create subscriptions for members at a center."),
            (PermissionCodes.SubscriptionRead, "View subscriptions", "View own or authorized center subscription history."),
            (PermissionCodes.PaymentSelfCreate, "Start own payment", "Start an online payment for the authenticated member."),
            (PermissionCodes.PaymentCenterCash, "Record center payment", "Record an authorized center payment."),
            (PermissionCodes.AuditCenterRead, "View center audit history", "View operational audit events for an assigned center."),
            (PermissionCodes.RolePermissionManage, "Manage role permissions", "Replace permissions assigned to system roles.")
        };

        foreach (var definition in permissionDefinitions)
        {
            if (await context.Permissions.AnyAsync(permission => permission.Code == definition.Code))
            {
                continue;
            }

            context.Permissions.Add(new Permission
            {
                Code = definition.Code,
                Name = definition.Name,
                Description = definition.Description
            });
        }
        await context.SaveChangesAsync();

        // Existing deployments keep an administrator's edited matrix; defaults seed a fresh role matrix once.
        if (!await context.RolePermissions.AnyAsync())
        {
            var memberCodes = new[]
            {
                PermissionCodes.MemberSelfRead,
                PermissionCodes.MemberSelfUpdate,
                PermissionCodes.SubscriptionSelfCreate,
                PermissionCodes.SubscriptionRead,
                PermissionCodes.PaymentSelfCreate
            };
            var managerCodes = new[]
            {
                PermissionCodes.MemberCenterRead,
                PermissionCodes.MemberCenterCreate,
                PermissionCodes.MemberCenterUpdate,
                PermissionCodes.MemberCenterManageStatus,
                PermissionCodes.StaffCenterRead,
                PermissionCodes.StaffCenterCreate,
                PermissionCodes.StaffCenterUpdate,
                PermissionCodes.MembershipPackagesManage,
                PermissionCodes.SubscriptionCenterCreate,
                PermissionCodes.SubscriptionRead,
                PermissionCodes.PaymentCenterCash,
                PermissionCodes.AuditCenterRead
            };
            var receptionistCodes = new[]
            {
                PermissionCodes.MemberCenterRead,
                PermissionCodes.MemberCenterCreate,
                PermissionCodes.SubscriptionCenterCreate,
                PermissionCodes.SubscriptionRead,
                PermissionCodes.PaymentCenterCash
            };

            var permissionIds = await context.Permissions
                .ToDictionaryAsync(permission => permission.Code, permission => permission.Id);
            var roleIds = await context.Roles
                .ToDictionaryAsync(role => role.Name, role => role.Id);
            var grants = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [RoleNames.Member] = memberCodes,
                [RoleNames.Coach] = [],
                [RoleNames.Manager] = managerCodes,
                [RoleNames.Receptionist] = receptionistCodes,
                [RoleNames.SystemAdmin] = [PermissionCodes.RolePermissionManage]
            };

            foreach (var (roleName, codes) in grants)
            {
                if (!roleIds.TryGetValue(roleName, out var roleId))
                {
                    continue;
                }

                foreach (var code in codes)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = roleId,
                        PermissionId = permissionIds[code]
                    });
                }
            }
            await context.SaveChangesAsync();
        }

        var systemAdmin = await context.Roles.SingleAsync(role => role.Name == RoleNames.SystemAdmin);
        var permissionManage = await context.Permissions.SingleAsync(
            permission => permission.Code == PermissionCodes.RolePermissionManage);
        var invalidRolePermissionGrants = await context.RolePermissions
            .Where(link => link.PermissionId == permissionManage.Id && link.RoleId != systemAdmin.Id)
            .ToListAsync();
        if (invalidRolePermissionGrants.Count > 0)
        {
            context.RolePermissions.RemoveRange(invalidRolePermissionGrants);
        }
        if (!await context.RolePermissions.AnyAsync(link =>
                link.RoleId == systemAdmin.Id && link.PermissionId == permissionManage.Id))
        {
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = systemAdmin.Id,
                PermissionId = permissionManage.Id
            });
        }
        await context.SaveChangesAsync();
    }
}

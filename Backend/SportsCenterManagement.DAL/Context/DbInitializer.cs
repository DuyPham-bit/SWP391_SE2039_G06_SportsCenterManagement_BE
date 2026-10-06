using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>
/// Khởi tạo dữ liệu mẫu cho Database nếu bảng đang trống để phục vụ test tính năng.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context, bool baselineLegacySchema = false)
    {
        if (baselineLegacySchema)
        {
            await BaselineLegacySchemaAsync(context);
        }

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // 1. Tạo Center mẫu nếu chưa có
        if (!await context.Centers.AnyAsync())
        {
            var center = new Center
            {
                Name = "Sports Center Quận 1",
                Address = "123 Nguyễn Thị Minh Khai, Quận 1, TP.HCM",
                Phone = "0901234567",
                Email = "center.q1@sportscenter.vn",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Centers.AddAsync(center);
            await context.SaveChangesAsync();

            // 2. Tạo Gói tập mẫu (Membership Package)
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
                        Description = "Toàn quyền sử dụng dịch vụ và huấn luyện viên trong 90 ngày",
                        DurationDays = 90,
                        Price = 1200000,
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    }
                );
                await context.SaveChangesAsync();
            }

            // 3. Tạo Role & User Member mẫu
            if (!await context.Roles.AnyAsync(r => r.Name == RoleNames.Member || r.Name == "Member"))
            {
                var role = new Role
                {
                    Name = RoleNames.Member,
                    Description = "Khách hàng hội viên",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Roles.AddAsync(role);
                await context.SaveChangesAsync();

                var user = new User
                {
                    RoleId = role.Id,
                    Username = "member01",
                    Email = "member@scms.vn",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    Phone = "0912345678",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Users.AddAsync(user);
                await context.SaveChangesAsync();

                var memberProfile = new MemberProfile
                {
                    UserId = user.Id,
                    CenterId = center.Id,
                    MemberCode = "MB00001",
                    FullName = "Nguyễn Văn A",
                    CreatedAt = DateTime.UtcNow
                };
                await context.MemberProfiles.AddAsync(memberProfile);
                await context.SaveChangesAsync();
            }
        }

        // Roles
        foreach (var roleName in new[]
                 {
                     RoleNames.Member, RoleNames.Coach, RoleNames.Manager,
                     RoleNames.Receptionist, RoleNames.SystemAdmin, "Admin"
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
                    "Admin" => "Quản trị viên hệ thống",
                    _ => "Nhân sự trung tâm"
                },
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        // Permissions
        var permissionDefinitions = new (string Code, string Name, string Description, string? Module)[]
        {
            (PermissionCodes.MemberSelfRead, "View own profile", "View the authenticated member's own profile.", "MEMBERS"),
            (PermissionCodes.MemberSelfUpdate, "Update own profile", "Update the authenticated member's own profile.", "MEMBERS"),
            (PermissionCodes.MemberCenterRead, "View center members", "Search and view members assigned to a center.", "MEMBERS"),
            (PermissionCodes.MemberCenterCreate, "Create center members", "Register members at a center.", "MEMBERS"),
            (PermissionCodes.MemberCenterUpdate, "Update center members", "Update member profile details at a center.", "MEMBERS"),
            (PermissionCodes.MemberCenterManageStatus, "Manage member status", "Activate or disable a member account.", "MEMBERS"),
            (PermissionCodes.StaffCenterRead, "View center staff", "View coach and staff accounts assigned to a center.", "STAFF"),
            (PermissionCodes.StaffCenterCreate, "Create center staff", "Create coach and receptionist accounts at a center.", "STAFF"),
            (PermissionCodes.StaffCenterUpdate, "Update center staff", "Update center staff profiles and statuses.", "STAFF"),
            (PermissionCodes.MembershipPackagesManage, "Manage membership packages", "Create and update membership packages.", "PACKAGES"),
            (PermissionCodes.SubscriptionSelfCreate, "Buy own subscription", "Create a subscription for the authenticated member.", "SUBSCRIPTIONS"),
            (PermissionCodes.SubscriptionCenterCreate, "Sell subscriptions", "Create subscriptions for members at a center.", "SUBSCRIPTIONS"),
            (PermissionCodes.SubscriptionRead, "View subscriptions", "View own or authorized center subscription history.", "SUBSCRIPTIONS"),
            (PermissionCodes.PaymentSelfCreate, "Start own payment", "Start an online payment for the authenticated member.", "PAYMENTS"),
            (PermissionCodes.PaymentCenterCash, "Record center payment", "Record an authorized center payment.", "PAYMENTS"),
            (PermissionCodes.AuditCenterRead, "View center audit history", "View operational audit events for an assigned center.", "AUDIT"),
            (PermissionCodes.RolePermissionManage, "Manage role permissions", "Replace permissions assigned to system roles.", "ROLES"),
            ("CLASS_VIEW", "Xem lớp học", "View class data", "CLASSES"),
            ("CLASS_MANAGE", "Quản lý lớp học", "Create and update classes", "CLASSES"),
            ("COACH_ASSIGN", "Phân công huấn luyện viên", "Assign coaches to classes", "CLASSES"),
            ("MEMBER_VIEW", "Xem hội viên", "View member data", "MEMBERS"),
            ("MEMBER_MANAGE", "Quản lý hội viên", "Create and update members", "MEMBERS"),
            ("ROLE_MANAGE", "Quản lý vai trò và quyền", "Manage roles and permissions", "ROLES")
        };

        foreach (var def in permissionDefinitions)
        {
            var existing = await context.Permissions.SingleOrDefaultAsync(p => p.Code == def.Code);
            if (existing is null)
            {
                context.Permissions.Add(new Permission
                {
                    Code = def.Code,
                    Name = def.Name,
                    Description = def.Description,
                    Module = def.Module
                });
            }
            else if (existing.Module is null && def.Module is not null)
            {
                existing.Module = def.Module;
            }
        }
        await context.SaveChangesAsync();

        // Default RolePermissions
        if (!await context.RolePermissions.AnyAsync())
        {
            var memberCodes = new[]
            {
                PermissionCodes.MemberSelfRead,
                PermissionCodes.MemberSelfUpdate,
                PermissionCodes.SubscriptionSelfCreate,
                PermissionCodes.SubscriptionRead,
                PermissionCodes.PaymentSelfCreate,
                "CLASS_VIEW"
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
                PermissionCodes.AuditCenterRead,
                "CLASS_VIEW", "CLASS_MANAGE", "COACH_ASSIGN", "MEMBER_VIEW", "MEMBER_MANAGE"
            };
            var receptionistCodes = new[]
            {
                PermissionCodes.MemberCenterRead,
                PermissionCodes.MemberCenterCreate,
                PermissionCodes.SubscriptionCenterCreate,
                PermissionCodes.SubscriptionRead,
                PermissionCodes.PaymentCenterCash,
                "CLASS_VIEW", "MEMBER_VIEW"
            };

            var permissionIds = await context.Permissions
                .ToDictionaryAsync(permission => permission.Code, permission => permission.Id);
            var roleIds = await context.Roles
                .ToDictionaryAsync(role => role.Name, role => role.Id, StringComparer.OrdinalIgnoreCase);

            var grants = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [RoleNames.Member] = memberCodes,
                ["Member"] = memberCodes,
                [RoleNames.Coach] = ["CLASS_VIEW"],
                ["Coach"] = ["CLASS_VIEW"],
                [RoleNames.Manager] = managerCodes,
                ["Manager"] = managerCodes,
                [RoleNames.Receptionist] = receptionistCodes,
                ["Receptionist"] = receptionistCodes,
                [RoleNames.SystemAdmin] = [PermissionCodes.RolePermissionManage, "ROLE_MANAGE"],
                ["Admin"] = managerCodes.Concat(new[] { PermissionCodes.RolePermissionManage, "ROLE_MANAGE" }).Distinct().ToArray()
            };

            foreach (var (roleName, codes) in grants)
            {
                if (!roleIds.TryGetValue(roleName, out var roleId))
                {
                    continue;
                }

                foreach (var code in codes)
                {
                    if (permissionIds.TryGetValue(code, out var permId))
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = roleId,
                            PermissionId = permId
                        });
                    }
                }
            }
            await context.SaveChangesAsync();
        }

        // Admin User seed
        var adminRole = await context.Roles.FirstOrDefaultAsync(role => role.Name == "Admin" || role.Name == RoleNames.SystemAdmin);
        if (adminRole is not null && !await context.Users.AnyAsync(user => user.Email == "admin@sportscenter.vn"))
        {
            await context.Users.AddAsync(new User
            {
                RoleId = adminRole.Id,
                Username = "admin",
                Email = "admin@sportscenter.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var seededMemberUser = await context.Users.SingleOrDefaultAsync(user => user.Username == "member01");
        if (seededMemberUser is not null && !seededMemberUser.PasswordHash.StartsWith("$2", StringComparison.Ordinal))
        {
            seededMemberUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
            seededMemberUser.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        var systemAdminRole = await context.Roles.FirstOrDefaultAsync(role => role.Name == RoleNames.SystemAdmin);
        var permManage = await context.Permissions.FirstOrDefaultAsync(
            permission => permission.Code == PermissionCodes.RolePermissionManage);
        if (systemAdminRole is not null && permManage is not null)
        {
            if (!await context.RolePermissions.AnyAsync(link =>
                    link.RoleId == systemAdminRole.Id && link.PermissionId == permManage.Id))
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = systemAdminRole.Id,
                    PermissionId = permManage.Id
                });
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task BaselineLegacySchemaAsync(SportsCenterDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE
                WHEN OBJECT_ID(N'[dbo].[centers]', N'U') IS NULL THEN 0
                WHEN OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL THEN 1
                WHEN NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory]) THEN 2
                ELSE 0
            END
            """;

        var state = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (state == 0)
        {
            return;
        }

        if (state == 1)
        {
            await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE [dbo].[__EFMigrationsHistory] (
                [MigrationId] nvarchar(150) NOT NULL,
                [ProductVersion] nvarchar(32) NOT NULL,
                CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
            );
            """);
        }

        await context.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (
                SELECT 1 FROM [dbo].[__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260929152157_InitialCreate')
            BEGIN
                INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260929152157_InitialCreate', N'10.0.12');
            END

            IF NOT EXISTS (
                SELECT 1 FROM [dbo].[__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260929165224_CoreFlowUniqueness')
            BEGIN
                INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260929165224_CoreFlowUniqueness', N'10.0.12');
            END
            """);
    }
}

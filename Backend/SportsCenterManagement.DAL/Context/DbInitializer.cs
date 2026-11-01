using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>Initializes schema and required authorization data only.</summary>
public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context, bool baselineLegacySchema = false)
    {
        if (baselineLegacySchema) await BaselineLegacySchemaAsync(context);
        if (context.Database.IsRelational()) await context.Database.MigrateAsync();
        else await context.Database.EnsureCreatedAsync();
        await EnsureRolesAndPermissionsAsync(context);
        await EnsureAuthAndReceptionPermissionsAsync(context);
        await EnsureCompleteSportsCatalogAsync(context);
        await EnsureMembershipPackageSportsAsync(context);
    }

    private static async Task EnsureRolesAndPermissionsAsync(SportsCenterDbContext context)
    {
        // Roles
        foreach (var roleName in new[]
                 {
                     RoleNames.Member, RoleNames.Coach, RoleNames.Manager,
                     RoleNames.Receptionist, RoleNames.Admin
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
                    RoleNames.Admin => "Quản trị viên toàn hệ thống",
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
                [RoleNames.Admin] = [PermissionCodes.RolePermissionManage, "ROLE_MANAGE"]
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

        await ConsolidateLegacySystemAdminAsync(context);

        var adminRole = await context.Roles.SingleAsync(role => role.Name == RoleNames.Admin);
        var permManage = await context.Permissions.FirstOrDefaultAsync(
            permission => permission.Code == PermissionCodes.RolePermissionManage);
        if (permManage is not null)
        {
            if (!await context.RolePermissions.AnyAsync(link =>
                    link.RoleId == adminRole.Id && link.PermissionId == permManage.Id))
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permManage.Id
                });
                await context.SaveChangesAsync();
            }

            // Role-management belongs to the single Admin role only. This also removes grants left by legacy seeds.
            var nonAdminRoleIds = await context.Roles
                .Where(role => role.Id != adminRole.Id)
                .Select(role => role.Id)
                .ToListAsync();
            var invalidGrants = await context.RolePermissions
                .Where(link => nonAdminRoleIds.Contains(link.RoleId) && link.PermissionId == permManage.Id)
                .ToListAsync();
            if (invalidGrants.Count > 0)
            {
                context.RolePermissions.RemoveRange(invalidGrants);
                await context.SaveChangesAsync();
            }
        }

    }

    /// <summary>
    /// Upgrades installations that still have the legacy SystemAdmin role. The upgrade is intentionally
    /// fail-closed: an ambiguous installation with more than one privileged account must be resolved by
    /// an operator instead of silently choosing an Admin account.
    /// </summary>
    private static async Task ConsolidateLegacySystemAdminAsync(SportsCenterDbContext context)
    {
        const string legacyRoleName = "SystemAdmin";
        var adminRole = await context.Roles.SingleAsync(role => role.Name == RoleNames.Admin);
        var legacyRole = await context.Roles.SingleOrDefaultAsync(role => role.Name == legacyRoleName);
        if (legacyRole is null)
        {
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        var privilegedUserIds = await context.Users
            .Where(user => user.RoleId == adminRole.Id || user.RoleId == legacyRole.Id)
            .Select(user => user.Id)
            .ToArrayAsync();
        if (privilegedUserIds.Length > 1)
        {
            throw new InvalidOperationException(
                "Phát hiện nhiều tài khoản Admin/SystemAdmin cũ. Hãy giữ lại đúng một tài khoản trước khi khởi động lại hệ thống.");
        }

        if (privilegedUserIds.Length == 1)
        {
            var legacyUsers = await context.Users.Where(user => user.RoleId == legacyRole.Id).ToListAsync();
            foreach (var user in legacyUsers)
            {
                user.RoleId = adminRole.Id;
                user.AuthVersion++;
                user.UpdatedAt = DateTime.UtcNow;
            }
        }

        var legacyPermissions = await context.RolePermissions
            .Where(link => link.RoleId == legacyRole.Id)
            .ToListAsync();
        var adminPermissionIds = await context.RolePermissions
            .Where(link => link.RoleId == adminRole.Id)
            .Select(link => link.PermissionId)
            .ToListAsync();
        foreach (var legacyPermission in legacyPermissions.Where(link => !adminPermissionIds.Contains(link.PermissionId)))
        {
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = legacyPermission.PermissionId
            });
        }
        context.RolePermissions.RemoveRange(legacyPermissions);
        context.Roles.Remove(legacyRole);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task EnsureAuthAndReceptionPermissionsAsync(SportsCenterDbContext context)
    {
        const string seedMarker = "seed.rbac.auth-and-reception-search.v2";
        if (await context.SystemSettings.AnyAsync(setting => setting.SettingKey == seedMarker))
        {
            return;
        }

        var grantSpecs = new (string RoleName, string PermissionCode, string Name, string Description)[]
        {
            (RoleNames.Receptionist, PermissionCodes.MemberCenterRead,
                "View center members", "Search and view members assigned to a center."),
            (RoleNames.Member, PermissionCodes.MemberSelfRead,
                "View own profile", "View the authenticated member's own profile."),
            (RoleNames.Member, PermissionCodes.SubscriptionRead,
                "View subscriptions", "View subscriptions owned by the authenticated member.")
        };

        var roleNames = grantSpecs.Select(spec => spec.RoleName).Distinct().ToArray();
        var roles = await context.Roles
            .Where(role => roleNames.Contains(role.Name))
            .ToDictionaryAsync(role => role.Name);
        var missingRoles = roleNames.Where(roleName => !roles.ContainsKey(roleName)).ToArray();
        if (missingRoles.Length > 0)
        {
            throw new InvalidOperationException($"Role chưa được khởi tạo: {string.Join(", ", missingRoles)}.");
        }

        var permissionCodes = grantSpecs.Select(spec => spec.PermissionCode).Distinct().ToArray();
        var permissions = await context.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code);
        foreach (var spec in grantSpecs)
        {
            if (!permissions.ContainsKey(spec.PermissionCode))
            {
                var permission = new Permission
                {
                    Code = spec.PermissionCode,
                    Name = spec.Name,
                    Description = spec.Description,
                    Module = "MEMBERS"
                };
                context.Permissions.Add(permission);
                permissions.Add(spec.PermissionCode, permission);
            }
        }
        await context.SaveChangesAsync();

        foreach (var spec in grantSpecs)
        {
            var roleId = roles[spec.RoleName].Id;
            var permissionId = permissions[spec.PermissionCode].Id;
            if (await context.RolePermissions.AnyAsync(link =>
                    link.RoleId == roleId && link.PermissionId == permissionId))
            {
                continue;
            }

            context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }

        context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = seedMarker,
            SettingValue = "applied",
            Description = "One-time backfill for member sign-in profile access and receptionist member search."
        });
        await context.SaveChangesAsync();
    }

    private static async Task BaselineLegacySchemaAsync(SportsCenterDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN OBJECT_ID(N'[dbo].[centers]', N'U') IS NULL THEN 0 ELSE 1 END
            """;

        var hasLegacySchema = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        if (!hasLegacySchema)
        {
            return;
        }

        command.CommandText = """
            SELECT CASE WHEN OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL THEN 0 ELSE 1 END
            """;
        var hasMigrationHistory = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        if (!hasMigrationHistory)
        {
            await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE [dbo].[__EFMigrationsHistory] (
                [MigrationId] nvarchar(150) NOT NULL,
                [ProductVersion] nvarchar(32) NOT NULL,
                CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
            );
            """);
        }

        // A legacy database can have a partially populated history table; restore both baseline markers before applying later migrations.
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
    private static async Task EnsureCompleteSportsCatalogAsync(SportsCenterDbContext context)
    {
        var center = await context.Centers.FirstOrDefaultAsync(c => c.Id == 15)
                     ?? await context.Centers.FirstOrDefaultAsync();
        if (center == null) return;

        // Definition of all 10 sports and corresponding facilities matching site.landing-content
        var catalog = new (string SportName, string SportDesc, string RoomName, string RoomType, int RoomCapacity, string RoomLocation)[]
        {
            ("Bơi lội", "Bơi lội thể thao, cải thiện thể lực và sức bền toàn diện", "Bể bơi Olympic 50m", "Swimming Pool", 30, "Khu Thể Thao Dưới Nước • Tầng 1"),
            ("Gym & Fitness", "Rèn luyện thể hình, tăng cơ giảm mỡ và tăng cường sức mạnh", "Phòng Gym High-Performance", "Gym", 45, "Khu A • Tầng 2"),
            ("Yoga & Pilates", "Phục hồi chức năng, tăng độ dẻo dai và tĩnh tâm", "Phòng Yoga & Pilates Reformer", "Studio", 20, "Khu C • Tầng 3"),
            ("Cầu lông", "Tập luyện phản xạ nhanh, di chuyển linh hoạt và đối kháng", "Sân Cầu lông Tiêu chuẩn #1", "Badminton Court", 24, "Khu B • Tầng 2"),
            ("Boxing & Võ thuật", "Đốt cháy calo, rèn luyện kỹ năng tự vệ và phản xạ chiến đấu", "Võ đài Boxing & Kickfit", "Combat Ring", 25, "Khu C • Tầng 1"),
            ("Golf", "Học viện đào tạo Golf chuẩn quốc tế, driving range 18 làn và phòng mô phỏng 3D TrackMan 4K", "Tổ Hợp Sân Tập Golf & 3D Simulator", "Golf Complex", 30, "Khu Ngoài Trời & Khu C • Tầng 2"),
            ("Bóng đá", "Sân cỏ nhân tạo 5 người tiêu chuẩn, mặt cỏ êm ái chống chấn thương, hệ thống thoát nước và dàn đèn LED cao áp ban đêm.", "Sân Bóng Đá Cỏ Nhân Tạo (Sân 5)", "Outdoor Field", 14, "Khu Ngoài Trời"),
            ("Bóng rổ", "Sân bóng rổ tiêu chuẩn thi đấu phong trào, mặt sân sơn acrylic chống trượt, trụ rổ kính cường lực độ nảy tốt và mái che mát.", "Sân Bóng Rổ Tiêu Chuẩn Phong Trào", "Basketball Court", 25, "Khu B • Tầng 1"),
            ("Bóng bàn", "Khu bóng bàn trong nhà trang bị bàn thi đấu đạt chuẩn quốc tế, ánh sáng đều chống lóa và không gian di chuyển an toàn.", "Khu Bóng Bàn Trong Nhà", "Table Tennis Area", 20, "Khu B • Tầng 3"),
            ("Pickleball", "Cụm sân Pickleball ngoài trời hiện đại, mặt sân acrylic chuyên dụng, vạch bếp chuẩn thi đấu và rào chắn bóng kiên cố.", "Cụm Sân Pickleball Ngoài Trời", "Pickleball Complex", 20, "Khu Ngoài Trời")
        };

        var sportsDict = new Dictionary<string, Sport>(StringComparer.OrdinalIgnoreCase);
        var roomsDict = new Dictionary<string, Room>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in catalog)
        {
            var sport = await context.Sports.FirstOrDefaultAsync(s => s.Name == item.SportName);
            if (sport == null)
            {
                sport = new Sport
                {
                    CenterId = center.Id,
                    Name = item.SportName,
                    Description = item.SportDesc,
                    Status = "Active"
                };
                await context.Sports.AddAsync(sport);
                await context.SaveChangesAsync();
            }
            else if (sport.Status != "Active")
            {
                sport.Status = "Active";
                await context.SaveChangesAsync();
            }
            sportsDict[item.SportName] = sport;

            var room = await context.Rooms.FirstOrDefaultAsync(r => r.Name == item.RoomName);
            if (room == null)
            {
                room = new Room
                {
                    CenterId = center.Id,
                    Name = item.RoomName,
                    RoomType = item.RoomType,
                    Capacity = item.RoomCapacity,
                    LocationDescription = item.RoomLocation,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Rooms.AddAsync(room);
                await context.SaveChangesAsync();
            }
            else if (room.Status != "Active")
            {
                room.Status = "Active";
                await context.SaveChangesAsync();
            }
            roomsDict[item.RoomName] = room;
        }

        // Coach sports assignment
        var coaches = await context.CoachProfiles.ToListAsync();
        if (coaches.Count > 0)
        {
            var coachList = coaches.OrderBy(c => c.Id).ToList();
            var assignments = new (int CoachIndex, string SportName)[]
            {
                (0, "Golf"),
                (1, "Golf"),
                (2, "Bóng đá"),
                (3, "Bóng rổ"),
                (4, "Bóng bàn"),
                (4, "Pickleball")
            };

            foreach (var a in assignments)
            {
                var c = coachList.Count > a.CoachIndex ? coachList[a.CoachIndex] : coachList[0];
                if (sportsDict.TryGetValue(a.SportName, out var sp))
                {
                    if (!await context.CoachSports.AnyAsync(cs => cs.CoachId == c.Id && cs.SportId == sp.Id))
                    {
                        context.CoachSports.Add(new CoachSport { CoachId = c.Id, SportId = sp.Id });
                        await context.SaveChangesAsync();
                    }
                }
            }
        }

        // Ensure Golf Exercises
        if (sportsDict.TryGetValue("Golf", out var golfSport) && !await context.Exercises.AnyAsync(e => e.SportId == golfSport.Id))
        {
            context.Exercises.AddRange(
                new Exercise
                {
                    SportId = golfSport.Id,
                    CenterId = center.Id,
                    Name = "Golf Grip & Stance Chuẩn Quốc Tế",
                    Description = "Kỹ thuật cầm gậy Overlapping/Interlocking và thế đứng căn chỉnh trọng tâm.",
                    MuscleGroup = "Core & Full Body",
                    DifficultyLevel = "Beginner",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new Exercise
                {
                    SportId = golfSport.Id,
                    CenterId = center.Id,
                    Name = "Kỹ Thuật Full Swing Gậy Sắt 7 (Iron 7)",
                    Description = "Chuyển giao lực hông, giữ góc xoay vai và điểm tiếp xúc bóng lý tưởng.",
                    MuscleGroup = "Vai, Lưng, Hông",
                    DifficultyLevel = "Intermediate",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new Exercise
                {
                    SportId = golfSport.Id,
                    CenterId = center.Id,
                    Name = "Putting & Chipping Cảm Biến 3D TrackMan",
                    Description = "Luyện cảm giác gạt bóng trên green và cú chip tầm ngắn tiếp cận cờ hiệu quả.",
                    MuscleGroup = "Cổ tay & Thăng bằng",
                    DifficultyLevel = "All Levels",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureMembershipPackageSportsAsync(SportsCenterDbContext context)
    {
        var packages = await context.MembershipPackages.ToListAsync();
        bool changed = false;
        foreach (var pkg in packages)
        {
            var name = (pkg.Name ?? string.Empty).ToLowerInvariant();
            var access = (pkg.AccessType ?? string.Empty).ToLowerInvariant();
            int targetSports = pkg.AllowedSports;

            if (access.Contains("10") || access.Contains("15") || access.Contains("toàn quyền") || name.Contains("all-access") || name.Contains("olympic") || pkg.DurationDays >= 365)
            {
                targetSports = 10;
                if (pkg.AccessType != "Toàn quyền 10 môn")
                {
                    pkg.AccessType = "Toàn quyền 10 môn";
                    changed = true;
                }
            }
            else if (access.Contains("6") || name.Contains("elite") || pkg.DurationDays >= 180)
                targetSports = 6;
            else if (access.Contains("3") || name.Contains("pro") || pkg.DurationDays >= 90)
                targetSports = 3;
            else if (pkg.AllowedSports < 1)
                targetSports = 1;

            if (targetSports != pkg.AllowedSports)
            {
                pkg.AllowedSports = targetSports;
                changed = true;
            }
        }
        if (changed)
        {
            await context.SaveChangesAsync();
        }
    }
}

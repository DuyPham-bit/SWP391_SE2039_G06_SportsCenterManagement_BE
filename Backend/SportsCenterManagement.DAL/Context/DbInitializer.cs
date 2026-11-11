using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>Initializes schema and required authorization data only.</summary>
public static class DbInitializer
{
    // Timestamps are stored as UTC; session dates/times are Vietnam business time (UTC+7, no DST).
    private static DateTime BusinessToUtc(DateTime businessTime) => businessTime.AddHours(-7);

    public static async Task SeedAsync(SportsCenterDbContext context)
    {
        if (context.Database.IsRelational()) await context.Database.MigrateAsync();
        else await context.Database.EnsureCreatedAsync();
        await EnsureRolesAndPermissionsAsync(context);
        await EnsureAuthAndReceptionPermissionsAsync(context);
        await EnsureCompleteSportsCatalogAsync(context);
        await EnsureMembershipPackageSportsAsync(context);
        await EnsureDemoBusinessDataAsync(context);
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


    private static async Task EnsureDemoBusinessDataAsync(SportsCenterDbContext context)
    {
        const string markerKey = "seed.demo.business-data.v2";
        if (await context.SystemSettings.AnyAsync(s => s.SettingKey == markerKey))
        {
            return;
        }

        var center = await context.Centers.FirstOrDefaultAsync();
        if (center is null)
        {
            return;
        }

        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        var receptionist = await context.Users.FirstOrDefaultAsync(u => u.Username == "flow2.reception" || u.Username == "reception01");
        var primaryCoach = await context.CoachProfiles.FirstOrDefaultAsync();

        // 1. Ensure Standard Membership Packages (Basic, Pro, Elite, Olympic Pass)
        var standardPackages = new (string Name, string Description, int DurationDays, decimal Price, int? MaxClasses, string AccessType, int AllowedSports, string Badge, string Features)[]
        {
            (
                "Gói Basic Thể Thao",
                "Khởi động lối sống năng động với 1 bộ môn tiêu chuẩn và đầy đủ tiện nghi cơ bản.",
                30,
                650000m,
                10,
                "1 môn cơ bản",
                1,
                "Cơ bản",
                "[\"Tập luyện 1 bộ môn cơ bản\",\"Tủ locker tiêu chuẩn\",\"Miễn phí nước uống lọc và tắm nóng lạnh\"]"
            ),
            (
                "Gói Pro Bứt Phá",
                "Gói tập phổ biến nhất, tự chọn 3 bộ môn linh hoạt cùng quyền lợi đo InBody định kỳ.",
                90,
                1800000m,
                30,
                "3 môn tự chọn",
                3,
                "Phổ biến",
                "[\"Tự chọn 3 bộ môn linh hoạt\",\"Đo chỉ số InBody định kỳ hàng tháng\",\"Tủ locker thông minh VIP\",\"Khăn tập kháng khuẩn mỗi buổi\"]"
            ),
            (
                "Gói Elite Chuyên Nghiệp",
                "Dành cho người tập luyện chuyên sâu với 6 bộ môn, kèm 2 buổi tư vấn HLV cá nhân và xông hơi Sauna.",
                180,
                3200000m,
                70,
                "6 môn chuyên sâu",
                6,
                "Chuyên nghiệp",
                "[\"Toàn quyền 6 bộ môn chuyên sâu\",\"2 buổi tư vấn giáo án cùng HLV cá nhân\",\"Phòng xông hơi Sauna & Jacuzzi phục hồi\",\"Ưu tiên đặt chỗ trước giờ cao điểm\"]"
            ),
            (
                "Gói All-Access Olympic Pass",
                "Đặc quyền tối thượng toàn hệ thống: không giới hạn 10 môn thể thao Olympic, dịch vụ VIP và đưa đón.",
                365,
                5800000m,
                150,
                "Toàn quyền 10 môn",
                10,
                "VIP All-Access",
                "[\"Toàn quyền 10 môn thể thao Olympic\",\"Không giới hạn số lượt check-in\",\"Miễn phí dịch vụ khăn, locker & nước ion cao cấp\",\"Mời 1 bạn tập trải nghiệm miễn phí mỗi tháng\"]"
            )
        };

        foreach (var def in standardPackages)
        {
            var pkg = await context.MembershipPackages
                .FirstOrDefaultAsync(p => p.CenterId == center.Id && p.Name == def.Name);

            if (pkg is null)
            {
                pkg = new MembershipPackage
                {
                    CenterId = center.Id,
                    Name = def.Name,
                    Description = def.Description,
                    DurationDays = def.DurationDays,
                    Price = def.Price,
                    MaxClasses = def.MaxClasses,
                    AccessType = def.AccessType,
                    AllowedSports = def.AllowedSports,
                    Badge = def.Badge,
                    Features = def.Features,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow.AddMonths(-4)
                };
                await context.MembershipPackages.AddAsync(pkg);
                await context.SaveChangesAsync();
            }
        }

        // 2. Ensure Classes & Schedules across all active sports
        var sports = await context.Sports.ToListAsync();
        var rooms = await context.Rooms.Where(r => r.CenterId == center.Id).ToListAsync();
        var allCoaches = await context.CoachProfiles.ToListAsync();

        var classSeedDefs = new (string SportName, string ClassName, string RoomKeyword, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, string Level)[]
        {
            ("Gym", "Powerlifting & Hypertrophy Foundation", "Gym", 2, new TimeOnly(18, 0), new TimeOnly(19, 0), "Intermediate"),
            ("Yoga", "Hatha Yoga & Trị Liệu Cột Sống", "Yoga", 3, new TimeOnly(17, 30), new TimeOnly(18, 30), "All Levels"),
            ("Bơi lội", "Kỹ thuật Bơi Sải & Sức bền Olympic", "Bơi", 4, new TimeOnly(18, 0), new TimeOnly(19, 0), "Beginner"),
            ("Boxing", "Boxing Kháng Lực & Tự Vệ Thực Chiến", "Boxing", 5, new TimeOnly(19, 0), new TimeOnly(20, 0), "Intermediate"),
            ("Golf", "Golf Simulator & Kỹ Thuật Swing Chuẩn PGA", "Golf", 6, new TimeOnly(15, 0), new TimeOnly(16, 30), "Intermediate"),
            ("Cầu lông", "Cầu Lông Chiến Thuật & Footwork Nâng Cao", "Cầu lông", 2, new TimeOnly(19, 0), new TimeOnly(20, 30), "All Levels"),
            ("Bóng đá", "Futsal Kỹ Thuật Chạm Bóng & Di Chuyển Không Bóng", "Bóng đá", 3, new TimeOnly(19, 0), new TimeOnly(20, 30), "Beginner"),
            ("Bóng rổ", "Bóng Rổ Căn Bản & Ném Rổ Tầm Xa", "Bóng rổ", 4, new TimeOnly(18, 30), new TimeOnly(20, 0), "All Levels"),
            ("Bóng bàn", "Bóng Bàn Giao Cầu Xoáy & Đỡ Bóng Cắt", "Bóng bàn", 5, new TimeOnly(18, 0), new TimeOnly(19, 30), "All Levels"),
            ("Pickleball", "Pickleball Dinking & Phản Xạ Lưới Đỉnh Cao", "Pickleball", 6, new TimeOnly(17, 0), new TimeOnly(18, 30), "Beginner")
        };

        var nowUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc);
        int classIndex = 0;

        foreach (var def in classSeedDefs)
        {
            classIndex++;
            var sport = sports.FirstOrDefault(s => s.Name.Contains(def.SportName, StringComparison.OrdinalIgnoreCase));
            if (sport is null) continue;

            var room = rooms.FirstOrDefault(r => r.Name.Contains(def.RoomKeyword, StringComparison.OrdinalIgnoreCase)) ?? rooms.FirstOrDefault();
            var classEntity = await context.Classes.FirstOrDefaultAsync(c => c.CenterId == center.Id && c.Name == def.ClassName);

            if (classEntity is null)
            {
                classEntity = new ClassEntity
                {
                    CenterId = center.Id,
                    SportId = sport.Id,
                    RoomId = room?.Id,
                    Name = def.ClassName,
                    Description = $"Chương trình đào tạo bộ môn {sport.Name} chuẩn quốc tế tại trung tâm, nâng cao thể lực và phản xạ thi đấu.",
                    Level = def.Level,
                    Capacity = 20,
                    AllowWaitlist = true,
                    DurationMinutes = (int)(def.EndTime - def.StartTime).TotalMinutes,
                    Status = "Published",
                    CreatedAt = nowUtc.AddMonths(-3)
                };
                await context.Classes.AddAsync(classEntity);
                await context.SaveChangesAsync();

                if (allCoaches.Count > 0)
                {
                    var assignedCoach = allCoaches[classIndex % allCoaches.Count];
                    if (!await context.ClassCoaches.AnyAsync(cc => cc.ClassId == classEntity.Id && cc.CoachId == assignedCoach.Id))
                    {
                        context.ClassCoaches.Add(new ClassCoach
                        {
                            ClassId = classEntity.Id,
                            CoachId = assignedCoach.Id,
                            AssignedDate = today.AddMonths(-3),
                            IsPrimary = true
                        });
                        await context.SaveChangesAsync();
                    }
                }

                var schedule = new ClassSchedule
                {
                    ClassId = classEntity.Id,
                    RoomId = room?.Id,
                    DayOfWeek = def.DayOfWeek,
                    StartTime = def.StartTime,
                    EndTime = def.EndTime,
                    StartDate = today.AddMonths(-2),
                    EndDate = today.AddMonths(4),
                    Status = "Active"
                };
                await context.ClassSchedules.AddAsync(schedule);
                await context.SaveChangesAsync();

                for (var curDate = today.AddDays(-30); curDate <= today.AddDays(30); curDate = curDate.AddDays(1))
                {
                    if ((int)curDate.DayOfWeek == schedule.DayOfWeek)
                    {
                        var exists = await context.ClassSessions
                            .AnyAsync(s => s.ClassId == classEntity.Id && s.SessionDate == curDate && s.StartTime == schedule.StartTime);
                        if (!exists)
                        {
                            context.ClassSessions.Add(new ClassSession
                            {
                                ClassId = classEntity.Id,
                                ScheduleId = schedule.Id,
                                RoomId = schedule.RoomId,
                                CoachId = allCoaches.Count > 0 ? allCoaches[classIndex % allCoaches.Count].Id : null,
                                SessionDate = curDate,
                                StartTime = schedule.StartTime,
                                EndTime = schedule.EndTime,
                                SessionStatus = "Scheduled",
                                Notes = "Buổi học định kỳ theo giáo trình chuẩn",
                                CreatedAt = nowUtc.AddMonths(-2)
                            });
                        }
                    }
                }
                await context.SaveChangesAsync();
            }
        }

        // 3. Attach Subscriptions, Invoices, Payments, Bookings, Attendance, Checkins to EXISTING MEMBERS ONLY
        var activeUserIds = await context.Users
            .Where(u => u.Status == "Active")
            .Select(u => u.Id)
            .ToListAsync();

        var existingMembers = await context.MemberProfiles
            .Where(m => activeUserIds.Contains(m.UserId))
            .ToListAsync();

        if (existingMembers.Count > 0)
        {
            var allPackages = await context.MembershipPackages
                .Where(p => p.CenterId == center.Id && p.Status == "Active")
                .ToListAsync();

            var allPublishedClasses = await context.Classes
                .Where(c => c.CenterId == center.Id && c.Status == "Published")
                .ToListAsync();

            var staffUser = receptionist ?? adminUser;

            int memberCounter = 0;
            foreach (var member in existingMembers)
            {
                memberCounter++;
                var chosenPkg = allPackages[memberCounter % allPackages.Count];

                var pastStart = today.AddDays(-75);
                var pastEnd = pastStart.AddDays(chosenPkg.DurationDays > 60 ? 60 : chosenPkg.DurationDays);
                var activeStart = today.AddDays(-15);
                var activeEnd = activeStart.AddDays(chosenPkg.DurationDays);

                var subPairs = new[]
                {
                    (Start: pastStart, End: pastEnd, Status: "Expired", Created: nowUtc.AddDays(-75), IsRenewal: false),
                    (Start: activeStart, End: activeEnd, Status: "Active", Created: nowUtc.AddDays(-15), IsRenewal: true)
                };

                foreach (var subDef in subPairs)
                {
                    var existingSub = await context.MemberSubscriptions
                        .FirstOrDefaultAsync(s => s.MemberId == member.Id && s.StartDate == subDef.Start && s.PackageId == chosenPkg.Id);

                    MemberSubscription sub;
                    if (existingSub is null)
                    {
                        sub = new MemberSubscription
                        {
                            MemberId = member.Id,
                            PackageId = chosenPkg.Id,
                            StartDate = subDef.Start,
                            EndDate = subDef.End,
                            DurationDays = chosenPkg.DurationDays,
                            Price = chosenPkg.Price,
                            Status = subDef.Status,
                            AutoRenew = false,
                            CreatedAt = subDef.Created,
                            UpdatedAt = subDef.Created
                        };
                        await context.MemberSubscriptions.AddAsync(sub);
                        await context.SaveChangesAsync();
                    }
                    else
                    {
                        sub = existingSub;
                    }

                    var existingInvoiceItem = await context.InvoiceItems
                        .FirstOrDefaultAsync(ii => ii.SubscriptionId == sub.Id);

                    if (existingInvoiceItem is null)
                    {
                        var hexSuffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                        var invNumber = $"INV-{subDef.Created:yyyyMMdd}-{member.Id:D3}-{hexSuffix}";

                        var invoice = new Invoice
                        {
                            InvoiceNumber = invNumber,
                            MemberId = member.Id,
                            CenterId = center.Id,
                            CreatedBy = staffUser?.Id ?? member.UserId,
                            Subtotal = chosenPkg.Price,
                            Discount = 0,
                            Tax = 0,
                            TotalAmount = chosenPkg.Price,
                            Status = "Paid",
                            IssuedAt = subDef.Created,
                            PaidAt = subDef.Created,
                            IdempotencyKey = Guid.NewGuid().ToString("N")
                        };
                        await context.Invoices.AddAsync(invoice);
                        await context.SaveChangesAsync();

                        var invoiceItem = new InvoiceItem
                        {
                            InvoiceId = invoice.Id,
                            PackageId = chosenPkg.Id,
                            SubscriptionId = sub.Id,
                            Description = subDef.IsRenewal ? $"Gia hạn gói tập: {chosenPkg.Name}" : $"Đăng ký gói tập: {chosenPkg.Name}",
                            Quantity = 1,
                            UnitPrice = chosenPkg.Price,
                            Amount = chosenPkg.Price
                        };
                        await context.InvoiceItems.AddAsync(invoiceItem);

                        string method = ((member.Id + sub.Id) % 3) switch
                        {
                            0 => "VNPAY",
                            1 => "MOMO",
                            _ => "CASH"
                        };

                        var payment = new Payment
                        {
                            InvoiceId = invoice.Id,
                            MemberId = member.Id,
                            ProcessedBy = method == "CASH" ? staffUser?.Id : null,
                            PaymentMethod = method,
                            TransactionCode = $"{method}:TXN-{subDef.Created:yyyyMMdd}-{invoice.Id}-{hexSuffix}",
                            Amount = chosenPkg.Price,
                            AmountReceived = chosenPkg.Price,
                            PaymentStatus = "Succeeded",
                            PaidAt = subDef.Created,
                            AttemptedAt = subDef.Created.AddSeconds(-25),
                            CreatedAt = subDef.Created,
                            Note = method == "CASH" ? "Thu tiền mặt tại quầy lễ tân" : $"Thanh toán trực tuyến thành công qua cổng {method}"
                        };
                        await context.Payments.AddAsync(payment);
                        await context.SaveChangesAsync();
                    }

                    if (subDef.Status == "Active" && allPublishedClasses.Count > 0)
                    {
                        var assignedClasses = allPublishedClasses
                            .OrderBy(c => (c.Id * member.Id) % 17)
                            .Take(2)
                            .ToList();

                        foreach (var cls in assignedClasses)
                        {
                            var enrollment = await context.ClassEnrollments
                                .FirstOrDefaultAsync(e => e.ClassId == cls.Id && e.MemberId == member.Id);

                            if (enrollment is null)
                            {
                                enrollment = new ClassEnrollment
                                {
                                    ClassId = cls.Id,
                                    MemberId = member.Id,
                                    SubscriptionId = sub.Id,
                                    RegisteredBy = staffUser?.Id ?? member.UserId,
                                    RegisteredAt = subDef.Created,
                                    Status = "Active"
                                };
                                await context.ClassEnrollments.AddAsync(enrollment);
                                await context.SaveChangesAsync();
                            }

                            var sessions = await context.ClassSessions
                                .Where(s => s.ClassId == cls.Id && s.SessionDate >= subDef.Start && s.SessionDate <= today.AddDays(21))
                                .ToListAsync();

                            foreach (var sess in sessions)
                            {
                                var booking = await context.SessionBookings
                                    .FirstOrDefaultAsync(b => b.SessionId == sess.Id && b.MemberId == member.Id);

                                if (booking is null)
                                {
                                    booking = new SessionBooking
                                    {
                                        SessionId = sess.Id,
                                        MemberId = member.Id,
                                        EnrollmentId = enrollment.Id,
                                        SubscriptionId = sub.Id,
                                        RegisteredBy = staffUser?.Id,
                                        BookedAt = sess.SessionDate < today ? sess.SessionDate.ToDateTime(new TimeOnly(8, 0)).AddDays(-2) : nowUtc,
                                        Status = "Booked"
                                    };
                                    await context.SessionBookings.AddAsync(booking);
                                    await context.SaveChangesAsync();
                                }

                                if (sess.SessionDate < today)
                                {
                                    var hasAtt = await context.Attendances
                                        .AnyAsync(a => a.SessionId == sess.Id && a.MemberId == member.Id);

                                    if (!hasAtt)
                                    {
                                        bool isLate = (sess.Id + member.Id) % 5 == 0;
                                        var sessionStartDt = BusinessToUtc(sess.SessionDate.ToDateTime(sess.StartTime));
                                        var checkIn = isLate ? sessionStartDt.AddMinutes(10) : sessionStartDt.AddMinutes(-5);
                                        var checkOut = BusinessToUtc(sess.SessionDate.ToDateTime(sess.EndTime)).AddMinutes(5);

                                        context.Attendances.Add(new Attendance
                                        {
                                            SessionId = sess.Id,
                                            MemberId = member.Id,
                                            CheckedInBy = allCoaches.FirstOrDefault()?.UserId ?? staffUser?.Id,
                                            AttendanceStatus = isLate ? "LATE" : "PRESENT",
                                            CheckInTime = checkIn,
                                            CheckOutTime = checkOut,
                                            Note = isLate ? "Đến trễ 10 phút do kẹt xe" : "Điểm danh đúng giờ",
                                            CreatedAt = checkIn
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                for (int i = 1; i <= 10; i++)
                {
                    var checkDate = today.AddDays(-i * 2);
                    var cinTime = BusinessToUtc(checkDate.ToDateTime(new TimeOnly(17, 30))).AddMinutes((int)((member.Id * 7) % 60));
                    var coutTime = cinTime.AddMinutes(75);

                    if (!await context.CenterCheckins.AnyAsync(c => c.MemberId == member.Id && c.CheckInTime == cinTime))
                    {
                        context.CenterCheckins.Add(new CenterCheckin
                        {
                            MemberId = member.Id,
                            CenterId = center.Id,
                            CheckedInBy = staffUser?.Id,
                            CheckInTime = cinTime,
                            CheckOutTime = coutTime,
                            CreatedAt = cinTime
                        });
                    }
                }
                await context.SaveChangesAsync();
            }
        }

        // 4. Equipment issues
        if (!await context.EquipmentIssues.AnyAsync(e => e.CenterId == center.Id))
        {
            var reportUser = allCoaches.FirstOrDefault()?.UserId ?? adminUser?.Id ?? 1;
            context.EquipmentIssues.AddRange(
                new EquipmentIssue
                {
                    CenterId = center.Id,
                    ReportedByUserId = reportUser,
                    Title = "Dây cáp máy kéo xô đa năng có dấu hiệu sờn",
                    Description = "Dây cáp bên trái máy kéo lat pulldown tại tầng 2 bị sờn vỏ bọc, cần bảo dưỡng định kỳ.",
                    Severity = "Medium",
                    Status = "InProgress",
                    ReportedAt = nowUtc.AddDays(-4),
                    ResolutionNote = "Đã liên hệ nhà cung cấp phụ tùng thay thế."
                },
                new EquipmentIssue
                {
                    CenterId = center.Id,
                    ReportedByUserId = reportUser,
                    Title = "Hệ thống lọc nước hồ bơi tầng 1",
                    Description = "Cảm biến pH báo chỉ số nhẹ cần cân bằng Clo và lọc tuần hoàn ban đêm.",
                    Severity = "Low",
                    Status = "Resolved",
                    ReportedAt = nowUtc.AddDays(-10),
                    ResolvedAt = nowUtc.AddDays(-9),
                    ResolvedBy = adminUser?.Id,
                    ResolutionNote = "Đã sục rửa bình lọc cát và cân bằng nồng độ nước đạt chuẩn thi đấu."
                }
            );
            await context.SaveChangesAsync();
        }

        // 5. System setting marker
        context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = markerKey,
            SettingValue = "applied",
            Description = "Demo business data for existing members: packages, classes, invoices, payments, sessions, bookings, attendance, checkins.",
            UpdatedAt = nowUtc
        });
        await context.SaveChangesAsync();
    }

}

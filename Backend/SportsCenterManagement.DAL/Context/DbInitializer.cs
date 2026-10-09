using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>
/// Khởi tạo và đồng bộ dữ liệu mẫu cho Database theo đúng nghiệp vụ hệ thống.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context, bool baselineLegacySchema = false, bool seedDemoData = true)
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

        Center? center = null;
        if (seedDemoData)
        {
            // Chỉ tạo dữ liệu trung tâm và tài khoản mẫu trong Development.
            center = await context.Centers.FirstOrDefaultAsync();
            if (center == null)
            {
                center = new Center
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
            }

            // Gói tập mẫu chỉ được đồng bộ trong Development.
            var packagesToSync = new List<MembershipPackage>
            {
                new()
                {
                    CenterId = center.Id,
                    Name = "Gói Basic Thể Thao",
                    Description = "Rèn luyện 1 bộ môn tự chọn, phù hợp cho người mới bắt đầu hoặc lịch tập cố định.",
                    DurationDays = 30,
                    Price = 650000,
                    MaxClasses = 1,
                    AccessType = "1 môn tự chọn",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    CenterId = center.Id,
                    Name = "Gói Pro Bứt Phá",
                    Description = "Lựa chọn 3 bộ môn kết hợp (ví dụ: Gym + Bơi lội + Cầu lông), kèm 1 buổi kiểm tra InBody.",
                    DurationDays = 90,
                    Price = 1800000,
                    MaxClasses = 3,
                    AccessType = "3 môn tự chọn",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    CenterId = center.Id,
                    Name = "Gói Elite Chuyên Nghiệp",
                    Description = "Trải nghiệm thể thao đa năng toàn diện, hỗ trợ đặt sân ưu tiên và quyền vào phòng xông hơi Sauna.",
                    DurationDays = 180,
                    Price = 3200000,
                    MaxClasses = 6,
                    AccessType = "6 môn tự chọn",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    CenterId = center.Id,
                    Name = "Gói All-Access Olympic Pass",
                    Description = "Toàn quyền sử dụng 15 bộ môn và 9 sân thi đấu đẳng cấp quốc tế 365 ngày.",
                    DurationDays = 365,
                    Price = 5800000,
                    MaxClasses = 15,
                    AccessType = "Toàn quyền 15 môn",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                }
            };

            var existingPackages = await context.MembershipPackages.ToListAsync();
            foreach (var pkg in packagesToSync)
            {
                var existing = existingPackages.FirstOrDefault(p => p.Name == pkg.Name);
                if (existing != null)
                {
                    existing.Description = pkg.Description;
                    existing.DurationDays = pkg.DurationDays;
                    existing.Price = pkg.Price;
                    existing.MaxClasses = pkg.MaxClasses;
                    existing.AccessType = pkg.AccessType;
                    existing.Status = "Active";
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    await context.MembershipPackages.AddAsync(pkg);
                }
            }
            await context.SaveChangesAsync();
        }

        // Vai trò là dữ liệu nền cần có ở mọi môi trường.
        var defaultRoles = new List<Role>
        {
            new() { Name = "Admin", Description = "Quản trị viên toàn hệ thống", CreatedAt = DateTime.UtcNow },
            new() { Name = "Manager", Description = "Quản lý cơ sở trung tâm", CreatedAt = DateTime.UtcNow },
            new() { Name = "Receptionist", Description = "Nhân viên lễ tân tiếp đón & thanh toán", CreatedAt = DateTime.UtcNow },
            new() { Name = "Coach", Description = "Huấn luyện viên thể thao", CreatedAt = DateTime.UtcNow },
            new() { Name = "Member", Description = "Khách hàng hội viên", CreatedAt = DateTime.UtcNow }
        };

        foreach (var role in defaultRoles)
        {
            if (!await context.Roles.AnyAsync(r => r.Name == role.Name))
            {
                await context.Roles.AddAsync(role);
            }
        }
        await context.SaveChangesAsync();

        if (!seedDemoData)
        {
            await EnsureAuthAndReceptionPermissionsAsync(context);
            return;
        }

        var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
        var managerRole = await context.Roles.FirstAsync(r => r.Name == "Manager");
        var receptionistRole = await context.Roles.FirstAsync(r => r.Name == "Receptionist");
        var coachRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Coach");
        var memberRole = await context.Roles.FirstAsync(r => r.Name == "Member");
        var demoCenter = center ?? throw new InvalidOperationException("Development demo center was not initialized.");

        // 4. Tạo tài khoản Admin mặc định nếu chưa có
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminUser = new User
            {
                RoleId = adminRole.Id,
                Username = "admin",
                Email = "admin@sportscenter.vn",
                PasswordHash = HashPassword("Admin@123456"),
                Phone = "0900000001",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();
        }

        if (!await context.Users.AnyAsync(u => u.Username == "manager01"))
        {
            var managerUser = new User
            {
                RoleId = managerRole.Id,
                Username = "manager01",
                Email = "manager01@sportscenter.vn",
                PasswordHash = HashPassword("Manager@123456"),
                Phone = "0900000003",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(managerUser);
            await context.SaveChangesAsync();
        }

        var managerUserId = await context.Users
            .Where(user => user.Username == "manager01")
            .Select(user => user.Id)
            .SingleAsync();
        if (!await context.StaffProfiles.AnyAsync(profile => profile.UserId == managerUserId))
        {
            await context.StaffProfiles.AddAsync(new StaffProfile
            {
                UserId = managerUserId,
                CenterId = demoCenter.Id,
                StaffCode = $"STF-MANAGER-{managerUserId}",
                FullName = "Quản lý trung tâm",
                Position = "Manager",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // 5. Tạo tài khoản Lễ tân mặc định nếu chưa có
        if (!await context.Users.AnyAsync(u => u.Username == "reception01"))
        {
            var receptionUser = new User
            {
                RoleId = receptionistRole.Id,
                Username = "reception01",
                Email = "reception01@sportscenter.vn",
                PasswordHash = HashPassword("Reception@123456"),
                Phone = "0900000002",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(receptionUser);
            await context.SaveChangesAsync();
        }

        var receptionUserId = await context.Users
            .Where(user => user.Username == "reception01")
            .Select(user => user.Id)
            .SingleAsync();
        if (!await context.StaffProfiles.AnyAsync(profile => profile.UserId == receptionUserId))
        {
            await context.StaffProfiles.AddAsync(new StaffProfile
            {
                UserId = receptionUserId,
                CenterId = demoCenter.Id,
                StaffCode = $"STF-RECEPTION-{receptionUserId}",
                FullName = "Nhân viên Lễ tân",
                Position = "Receptionist",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // 5b. Tạo tài khoản Huấn luyện viên (Coach) mặc định nếu chưa có
        if (coachRole != null && !await context.Users.AnyAsync(u => u.Username == "coach01"))
        {
            var coachUser = new User
            {
                RoleId = coachRole.Id,
                Username = "coach01",
                Email = "coach01@sportscenter.vn",
                PasswordHash = HashPassword("Coach@123456"),
                Phone = "0900000004",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(coachUser);
            await context.SaveChangesAsync();

            await context.CoachProfiles.AddAsync(new CoachProfile
            {
                UserId = coachUser.Id,
                CenterId = demoCenter.Id,
                CoachCode = $"CCH-{coachUser.Id:D5}",
                FullName = "Huấn luyện viên mẫu",
                Specialization = "Gym & Fitness, Bơi lội",
                Certification = "NASM-CPT, Huấn luyện viên cấp 1",
                ExperienceYears = 5,
                Bio = "HLV giàu kinh nghiệm hỗ trợ cá nhân hóa bài tập",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // 6. Tạo tài khoản Member mẫu và MemberProfile nếu chưa có
        var memberUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "member01" || u.Email == "member@scms.vn");
        if (memberUser == null)
        {
            memberUser = new User
            {
                RoleId = memberRole.Id,
                Username = "member01",
                Email = "member@scms.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Phone = "0912345678",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(memberUser);
            await context.SaveChangesAsync();

            var memberProfile = new MemberProfile
            {
                UserId = memberUser.Id,
                CenterId = demoCenter.Id,
                MemberCode = "MB00001",
                FullName = "Nguyễn Văn A",
                Gender = "Nam",
                DateOfBirth = new DateOnly(1995, 5, 20),
                Address = "Quận 1, TP.HCM",
                FitnessGoal = "Tăng cơ, giảm mỡ, cải thiện sức bền",
                FitnessLevel = "Intermediate",
                HeightCm = 175,
                WeightKg = 70,
                CreatedAt = DateTime.UtcNow
            };
            await context.MemberProfiles.AddAsync(memberProfile);
            await context.SaveChangesAsync();
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
                ["Admin"] = managerCodes.Concat(new[] { "ROLE_MANAGE" }).Distinct().ToArray()
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
        adminRole = await context.Roles.FirstOrDefaultAsync(role => role.Name == "Admin" || role.Name == RoleNames.SystemAdmin);
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

        var seededAdmin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (seededAdmin != null && seededAdmin.PasswordHash.Contains(':'))
        {
            seededAdmin.PasswordHash = HashPassword("Admin@123456");
            seededAdmin.FailedLoginAttempts = 0;
            seededAdmin.LockedUntil = null;
            await context.SaveChangesAsync();
        }

        var seededManager = await context.Users.FirstOrDefaultAsync(u => u.Username == "manager01");
        if (seededManager != null && seededManager.PasswordHash.Contains(':'))
        {
            seededManager.PasswordHash = HashPassword("Manager@123456");
            seededManager.FailedLoginAttempts = 0;
            seededManager.LockedUntil = null;
            await context.SaveChangesAsync();
        }

        var seededReception = await context.Users.FirstOrDefaultAsync(u => u.Username == "reception01");
        if (seededReception != null && seededReception.PasswordHash.Contains(':'))
        {
            seededReception.PasswordHash = HashPassword("Reception@123456");
            seededReception.FailedLoginAttempts = 0;
            seededReception.LockedUntil = null;
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

            // Legacy seed data granted this permission to Admin/custom roles; the current policy reserves it for SystemAdmin.
            var nonSystemAdminRoleIds = await context.Roles
                .Where(role => role.Id != systemAdminRole.Id)
                .Select(role => role.Id)
                .ToListAsync();
            var invalidGrants = await context.RolePermissions
                .Where(link => nonSystemAdminRoleIds.Contains(link.RoleId) && link.PermissionId == permManage.Id)
                .ToListAsync();
            if (invalidGrants.Count > 0)
            {
                context.RolePermissions.RemoveRange(invalidGrants);
                await context.SaveChangesAsync();
            }
        }

        await EnsureAuthAndReceptionPermissionsAsync(context);
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

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            210_000,
            HashAlgorithmName.SHA256,
            32
        );
        return $"pbkdf2-sha256$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}

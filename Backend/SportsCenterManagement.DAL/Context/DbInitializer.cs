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

        if (!await context.Users.AnyAsync(u => u.Email == "member01@sportscenter.vn"))
        {
            var m1 = new User
            {
                RoleId = memberRole.Id,
                Username = "member_tuandm",
                Email = "member01@sportscenter.vn",
                PasswordHash = HashPassword("Member@123456"),
                Phone = "0900000006",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(m1);
            await context.SaveChangesAsync();

            var mp1 = new MemberProfile
            {
                UserId = m1.Id,
                CenterId = demoCenter.Id,
                MemberCode = "MEM-00001",
                FullName = "Phạm Minh Tuấn",
                Gender = "Nam",
                DateOfBirth = new DateOnly(1996, 8, 15),
                Address = "Quận 1, TP.HCM",
                FitnessGoal = "Tăng cơ, cải thiện sức bền",
                FitnessLevel = "Intermediate",
                HeightCm = 175,
                WeightKg = 71,
                CreatedAt = DateTime.UtcNow
            };
            await context.MemberProfiles.AddAsync(mp1);
            await context.SaveChangesAsync();

            var samplePkg = await context.MembershipPackages.FirstOrDefaultAsync(p => p.CenterId == demoCenter.Id && p.Status == "Active");
            if (samplePkg != null)
            {
                await context.MemberSubscriptions.AddAsync(new MemberSubscription
                {
                    MemberId = mp1.Id,
                    PackageId = samplePkg.Id,
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(samplePkg.DurationDays)),
                    Price = samplePkg.Price,
                    DurationDays = samplePkg.DurationDays,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });
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

        var seededCoach = await context.Users.FirstOrDefaultAsync(u => u.Username == "coach01");
        if (seededCoach != null && seededCoach.PasswordHash.Contains(':'))
        {
            seededCoach.PasswordHash = HashPassword("Coach@123456");
            seededCoach.FailedLoginAttempts = 0;
            seededCoach.LockedUntil = null;
            await context.SaveChangesAsync();
        }

        var seededMember = await context.Users.FirstOrDefaultAsync(u => u.Email == "member01@sportscenter.vn");
        if (seededMember != null && seededMember.PasswordHash.Contains(':'))
        {
            seededMember.PasswordHash = HashPassword("Member@123456");
            seededMember.FailedLoginAttempts = 0;
            seededMember.LockedUntil = null;
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

        await SeedDemoClassesMembersAndPlansAsync(context, demoCenter);
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

    private static async Task SeedDemoClassesMembersAndPlansAsync(SportsCenterDbContext context, Center demoCenter)
    {
        var memberRole = await context.Roles.FirstAsync(r => r.Name == "Member");
        var coachUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "coach01");
        var coachProfile = coachUser != null ? await context.CoachProfiles.FirstOrDefaultAsync(c => c.UserId == coachUser.Id) : null;
        var activePkg = await context.MembershipPackages.FirstOrDefaultAsync(p => p.CenterId == demoCenter.Id && p.Status == "Active")
                        ?? await context.MembershipPackages.FirstOrDefaultAsync(p => p.Status == "Active");

        // 1. Seed 5 Members
        var memberDefs = new (string Username, string Email, string Phone, string MemberCode, string FullName, string Gender, DateOnly Dob, string Address, string Goal, string Level, int Height, int Weight)[]
        {
            ("member_tuandm", "member01@sportscenter.vn", "0900000006", "MEM-00001", "Phạm Minh Tuấn", "Nam", new DateOnly(1996, 8, 15), "Quận 1, TP.HCM", "Tăng cơ, cải thiện sức bền", "Intermediate", 175, 71),
            ("member02", "member02@sportscenter.vn", "0900000007", "MEM-00002", "Hoàng Hải Yến", "Nữ", new DateOnly(1998, 3, 22), "Bình Thạnh, TP.HCM", "Giảm mỡ, săn chắc vóc dáng", "Beginner", 162, 52),
            ("member03", "member03@sportscenter.vn", "0900000008", "MEM-00003", "Đặng Quốc Bảo", "Nam", new DateOnly(1994, 11, 10), "Phú Nhuận, TP.HCM", "Tăng sức mạnh và độ linh hoạt", "Advanced", 178, 76),
            ("member04", "member04@sportscenter.vn", "0900000009", "MEM-00004", "Trần Thị Mai Anh", "Nữ", new DateOnly(2000, 5, 18), "Quận 3, TP.HCM", "Cải thiện nhịp tim và bơi lội", "Intermediate", 165, 54),
            ("member05", "member05@sportscenter.vn", "0900000010", "MEM-00005", "Vũ Đức Thắng", "Nam", new DateOnly(1997, 9, 5), "Quận 7, TP.HCM", "Phát triển thể lực và phản xạ boxing", "Intermediate", 172, 68)
        };

        var memberProfilesList = new List<MemberProfile>();

        foreach (var m in memberDefs)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == m.Email || u.Username == m.Username);
            if (user == null)
            {
                user = new User
                {
                    RoleId = memberRole.Id,
                    Username = m.Username,
                    Email = m.Email,
                    PasswordHash = HashPassword("Member@123456"),
                    Phone = m.Phone,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Users.AddAsync(user);
                await context.SaveChangesAsync();
            }
            else
            {
                user.Email = m.Email;
                user.Phone = m.Phone;
                user.Status = "Active";
                if (user.PasswordHash.Contains(':'))
                {
                    user.PasswordHash = HashPassword("Member@123456");
                }
                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
                await context.SaveChangesAsync();
            }

            var profile = await context.MemberProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new MemberProfile
                {
                    UserId = user.Id,
                    CenterId = demoCenter.Id,
                    MemberCode = m.MemberCode,
                    FullName = m.FullName,
                    Gender = m.Gender,
                    DateOfBirth = m.Dob,
                    Address = m.Address,
                    FitnessGoal = m.Goal,
                    FitnessLevel = m.Level,
                    HeightCm = m.Height,
                    WeightKg = m.Weight,
                    CreatedAt = DateTime.UtcNow
                };
                await context.MemberProfiles.AddAsync(profile);
                await context.SaveChangesAsync();
            }
            else
            {
                profile.FullName = m.FullName;
                profile.MemberCode = m.MemberCode;
                profile.Gender = m.Gender;
                profile.FitnessGoal = m.Goal;
                profile.FitnessLevel = m.Level;
                profile.HeightCm = m.Height;
                profile.WeightKg = m.Weight;
                await context.SaveChangesAsync();
            }

            memberProfilesList.Add(profile);

            if (activePkg != null && !await context.MemberSubscriptions.AnyAsync(s => s.MemberId == profile.Id && s.Status == "Active"))
            {
                await context.MemberSubscriptions.AddAsync(new MemberSubscription
                {
                    MemberId = profile.Id,
                    PackageId = activePkg.Id,
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(activePkg.DurationDays)),
                    Price = activePkg.Price,
                    DurationDays = activePkg.DurationDays,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }
        }

        // 2. Seed 5 Sports
        var sportsData = new (string Name, string Description)[]
        {
            ("Bơi lội", "Bơi lội thể thao, cải thiện thể lực và sức bền toàn diện"),
            ("Gym & Fitness", "Rèn luyện thể hình, tăng cơ giảm mỡ và tăng cường sức mạnh"),
            ("Yoga & Pilates", "Phục hồi chức năng, tăng độ dẻo dai và tĩnh tâm"),
            ("Cầu lông", "Tập luyện phản xạ nhanh, di chuyển linh hoạt và đối kháng"),
            ("Boxing & Võ thuật", "Đốt cháy calo, rèn luyện kỹ năng tự vệ và phản xạ chiến đấu")
        };

        var sportsMap = new Dictionary<string, Sport>();
        foreach (var s in sportsData)
        {
            var sport = await context.Sports.FirstOrDefaultAsync(sp => sp.Name == s.Name);
            if (sport == null)
            {
                sport = new Sport
                {
                    CenterId = demoCenter.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Status = "Active"
                };
                await context.Sports.AddAsync(sport);
                await context.SaveChangesAsync();
            }
            sportsMap[s.Name] = sport;
        }

        // 3. Seed 5 Rooms
        var roomsData = new (string Name, string RoomType, int Capacity, string Location)[]
        {
            ("Bể bơi Olympic 50m", "Swimming Pool", 30, "Tầng 1 - Khu liên hợp dưới nước"),
            ("Phòng Gym High-Performance", "Gym", 40, "Tầng 2 - Khu tập tạ chuyên sâu"),
            ("Phòng Yoga & Pilates Reformer", "Studio", 20, "Tầng 3 - Phòng Studio yên tĩnh"),
            ("Sân Cầu lông Tiêu chuẩn #1", "Badminton Court", 16, "Tầng 4 - Sân thảm Yonex chuyên dụng"),
            ("Võ đài Boxing & Kickfit", "Combat Ring", 20, "Tầng 2 - Khu võ thuật đối kháng")
        };

        var roomsMap = new Dictionary<string, Room>();
        foreach (var r in roomsData)
        {
            var room = await context.Rooms.FirstOrDefaultAsync(rm => rm.Name == r.Name && rm.CenterId == demoCenter.Id);
            if (room == null)
            {
                room = new Room
                {
                    CenterId = demoCenter.Id,
                    Name = r.Name,
                    RoomType = r.RoomType,
                    Capacity = r.Capacity,
                    LocationDescription = r.Location,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Rooms.AddAsync(room);
                await context.SaveChangesAsync();
            }
            roomsMap[r.Name] = room;
        }

        // 4. Seed 5 Classes
        var classesData = new (string Name, string SportName, string RoomName, string Level, int Capacity, int Duration, string Description)[]
        {
            ("Bơi sải Kỹ thuật Olympic K01", "Bơi lội", "Bể bơi Olympic 50m", "Intermediate", 15, 60, "Khóa huấn luyện kỹ thuật bơi sải tiêu chuẩn thi đấu, tối ưu sải tay và lực đạp chân"),
            ("Gym Tăng cơ Tối đa (Hypertrophy)", "Gym & Fitness", "Phòng Gym High-Performance", "All Levels", 20, 75, "Chương trình huấn luyện tăng kích thước cơ nạc, tối ưu chuyển động đòn bẩy và dinh dưỡng"),
            ("Yoga Phục hồi & Trị liệu Cột sống", "Yoga & Pilates", "Phòng Yoga & Pilates Reformer", "Beginner", 15, 60, "Các bài tập kéo giãn chuyên sâu giúp giải tỏa áp lực đốt sống cổ và thắt lưng"),
            ("Cầu lông Nâng cao Tấn công & Di chuyển", "Cầu lông", "Sân Cầu lông Tiêu chuẩn #1", "Advanced", 12, 90, "Rèn luyện bộ chân di chuyển 6 góc sân, hoàn thiện kỹ thuật đập cầu cắm và gài lưới"),
            ("Boxing & Kickfit Đốt mỡ Cường độ cao", "Boxing & Võ thuật", "Võ đài Boxing & Kickfit", "Intermediate", 16, 60, "Huấn luyện tổ hợp đòn đấm 1-2-3 kết hợp footwork né đòn, đốt cháy 600-800 calo/buổi")
        };

        var classesMap = new Dictionary<string, ClassEntity>();
        foreach (var c in classesData)
        {
            var cls = await context.Classes.FirstOrDefaultAsync(cl => cl.Name == c.Name && cl.CenterId == demoCenter.Id);
            if (cls == null)
            {
                cls = new ClassEntity
                {
                    CenterId = demoCenter.Id,
                    SportId = sportsMap[c.SportName].Id,
                    RoomId = roomsMap[c.RoomName].Id,
                    Name = c.Name,
                    Description = c.Description,
                    Level = c.Level,
                    Capacity = c.Capacity,
                    DurationMinutes = c.Duration,
                    Status = "Published",
                    AllowWaitlist = true,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Classes.AddAsync(cls);
                await context.SaveChangesAsync();
            }
            classesMap[c.Name] = cls;

            // Assign Coach
            if (coachProfile != null)
            {
                if (!await context.ClassCoaches.AnyAsync(cc => cc.ClassId == cls.Id && cc.CoachId == coachProfile.Id))
                {
                    await context.ClassCoaches.AddAsync(new ClassCoach
                    {
                        ClassId = cls.Id,
                        CoachId = coachProfile.Id,
                        AssignedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                        IsPrimary = true
                    });
                    await context.SaveChangesAsync();
                }
            }
        }

        // 5. Seed Class Schedules
        var scheduleDefs = new (string ClassName, string RoomName, int DayOfWeek, TimeOnly Start, TimeOnly End)[]
        {
            ("Bơi sải Kỹ thuật Olympic K01", "Bể bơi Olympic 50m", 1, new TimeOnly(7, 0), new TimeOnly(8, 0)),
            ("Bơi sải Kỹ thuật Olympic K01", "Bể bơi Olympic 50m", 3, new TimeOnly(7, 0), new TimeOnly(8, 0)),
            ("Bơi sải Kỹ thuật Olympic K01", "Bể bơi Olympic 50m", 5, new TimeOnly(7, 0), new TimeOnly(8, 0)),

            ("Gym Tăng cơ Tối đa (Hypertrophy)", "Phòng Gym High-Performance", 1, new TimeOnly(18, 0), new TimeOnly(19, 15)),
            ("Gym Tăng cơ Tối đa (Hypertrophy)", "Phòng Gym High-Performance", 3, new TimeOnly(18, 0), new TimeOnly(19, 15)),

            ("Yoga Phục hồi & Trị liệu Cột sống", "Phòng Yoga & Pilates Reformer", 2, new TimeOnly(8, 30), new TimeOnly(9, 30)),
            ("Yoga Phục hồi & Trị liệu Cột sống", "Phòng Yoga & Pilates Reformer", 4, new TimeOnly(8, 30), new TimeOnly(9, 30)),

            ("Cầu lông Nâng cao Tấn công & Di chuyển", "Sân Cầu lông Tiêu chuẩn #1", 2, new TimeOnly(19, 0), new TimeOnly(20, 30)),
            ("Cầu lông Nâng cao Tấn công & Di chuyển", "Sân Cầu lông Tiêu chuẩn #1", 4, new TimeOnly(19, 0), new TimeOnly(20, 30)),

            ("Boxing & Kickfit Đốt mỡ Cường độ cao", "Võ đài Boxing & Kickfit", 1, new TimeOnly(19, 30), new TimeOnly(20, 30)),
            ("Boxing & Kickfit Đốt mỡ Cường độ cao", "Võ đài Boxing & Kickfit", 3, new TimeOnly(19, 30), new TimeOnly(20, 30))
        };

        var schedulesList = new List<ClassSchedule>();
        foreach (var sd in scheduleDefs)
        {
            var cls = classesMap[sd.ClassName];
            var room = roomsMap[sd.RoomName];
            var sched = await context.ClassSchedules.FirstOrDefaultAsync(s => s.ClassId == cls.Id && s.DayOfWeek == sd.DayOfWeek && s.StartTime == sd.Start);
            if (sched == null)
            {
                sched = new ClassSchedule
                {
                    ClassId = cls.Id,
                    RoomId = room.Id,
                    DayOfWeek = sd.DayOfWeek,
                    StartTime = sd.Start,
                    EndTime = sd.End,
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                    EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)),
                    Status = "Active"
                };
                await context.ClassSchedules.AddAsync(sched);
                await context.SaveChangesAsync();
            }
            schedulesList.Add(sched);
        }

        // 6. Seed Upcoming Sessions (for 21 days from today)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var offset = 0; offset <= 21; offset++)
        {
            var sessionDate = today.AddDays(offset);
            var dayOfWeek = (int)sessionDate.DayOfWeek;

            foreach (var sched in schedulesList.Where(s => s.DayOfWeek == dayOfWeek))
            {
                if (!await context.ClassSessions.AnyAsync(cs => cs.ClassId == sched.ClassId && cs.SessionDate == sessionDate && cs.StartTime == sched.StartTime))
                {
                    await context.ClassSessions.AddAsync(new ClassSession
                    {
                        ClassId = sched.ClassId,
                        ScheduleId = sched.Id,
                        RoomId = sched.RoomId,
                        CoachId = coachProfile?.Id,
                        SessionDate = sessionDate,
                        StartTime = sched.StartTime,
                        EndTime = sched.EndTime,
                        SessionStatus = "Scheduled",
                        Notes = "Buổi học định kỳ",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }
        await context.SaveChangesAsync();

        // 7. Seed 5 Training Plans (Lessons)
        if (coachProfile != null && memberProfilesList.Count >= 5)
        {
            var planDefs = new (string Name, string ClassName, int MemberIndex, string PlanType, string Goal, string Description)[]
            {
                ("Giáo án Hoàn thiện kỹ thuật bơi sải & Nhịp thở Olympic", "Bơi sải Kỹ thuật Olympic K01", 3, "Class", "Tối ưu nhịp thở 2 bên, vươn sải tay xa, hoàn thành 500m bơi liên tục", "Giáo trình 8 tuần rèn luyện cơ tay sau, nhóm cơ liên sườn và kiểm soát nhịp thở dưới nước"),
                ("Giáo án Tăng phì đại cơ thân trên Hypertrophy Chu kỳ 1", "Gym Tăng cơ Tối đa (Hypertrophy)", 0, "Personal", "Tăng 2kg khối lượng cơ nạc thân trên, cải thiện Bench Press lên 80kg", "Lịch tập chia nhóm cơ Push/Pull, tập trung Bench Press, Overhead Press, Barbell Row và Dips"),
                ("Giáo án Yoga Trị liệu Cột sống & Mở khớp hông", "Yoga Phục hồi & Trị liệu Cột sống", 1, "Class", "Giải tỏa căng thẳng vùng thắt lưng, cải thiện tư thế đứng và độ dẻo dai khớp hông", "Các chuỗi động tác Cat-Cow, Pigeon Pose, Cobra Pose và các bài thở Pranayama phục hồi"),
                ("Giáo án Bộ chân di chuyển & Đập cầu góc hẹp Smash", "Cầu lông Nâng cao Tấn công & Di chuyển", 2, "Personal", "Nâng cao tốc độ hồi sân, tăng lực phát bóng và đập cầu chính xác góc lưới", "Bài tập bước di chuyển 6 góc sân, bài tập cổ tay với vợt nặng, thực chiến phản xạ giao cầu"),
                ("Giáo án Footwork né đòn & Tổ hợp đòn đấm 1-2-3 Boxing", "Boxing & Kickfit Đốt mỡ Cường độ cao", 4, "Personal", "Đốt cháy 600 kcal/buổi, hoàn thiện combo đòn Jab - Cross - Lead Hook uy lực", "Khởi động nhảy dây, đấm gió Shadow Boxing, bài tập né đòn Slip-Roll và đánh pad cường độ HIIT")
            };

            foreach (var pd in planDefs)
            {
                var cls = classesMap[pd.ClassName];
                var member = memberProfilesList[pd.MemberIndex];

                if (!await context.TrainingPlans.AnyAsync(tp => tp.Name == pd.Name))
                {
                    await context.TrainingPlans.AddAsync(new TrainingPlan
                    {
                        MemberId = member.Id,
                        ClassId = cls.Id,
                        CoachId = coachProfile.Id,
                        Name = pd.Name,
                        Description = pd.Description,
                        Goal = pd.Goal,
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50)),
                        PlanType = pd.PlanType,
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await context.SaveChangesAsync();
        }
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

using Microsoft.EntityFrameworkCore;
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
            if (!await context.Roles.AnyAsync(r => r.Name == "Member"))
            {
                var role = new Role
                {
                    Name = "Member",
                    Description = "Khách hàng hội viên",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Roles.AddAsync(role);
                await context.SaveChangesAsync();

<<<<<<< Updated upstream
<<<<<<< Updated upstream
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
=======
        // 3.1. Khởi tạo danh sách các Quyền hạn (Permissions) chuẩn
        var defaultPermissions = new List<Permission>
        {
            new() { Code = "CLASS_VIEW", Name = "Xem danh sách lớp học", Description = "Cho phép xem thông tin và danh mục các lớp học" },
            new() { Code = "CLASS_MANAGE", Name = "Quản lý lớp học", Description = "Cho phép tạo, cập nhật, xuất bản lớp học" },
            new() { Code = "COACH_ASSIGN", Name = "Phân công huấn luyện viên", Description = "Cho phép phân công HLV vào lớp học" },
            new() { Code = "MEMBER_VIEW", Name = "Xem danh sách hội viên", Description = "Cho phép xem thông tin hội viên" },
            new() { Code = "MEMBER_MANAGE", Name = "Quản lý hội viên", Description = "Cho phép tạo, sửa, cập nhật hồ sơ hội viên" },
            new() { Code = "ROLE_MANAGE", Name = "Quản lý vai trò & quyền hạn", Description = "Cho phép cấu hình phân quyền cho các vai trò hệ thống" },
            new() { Code = "PAYMENT_PROCESS", Name = "Xử lý thanh toán", Description = "Cho phép thu tiền, lập hóa đơn, xử lý thanh toán" },
            new() { Code = "REPORT_VIEW", Name = "Xem báo cáo doanh thu & vận hành", Description = "Cho phép xem báo cáo thống kê" }
        };
>>>>>>> Stashed changes

        foreach (var perm in defaultPermissions)
        {
            if (!await context.Permissions.AnyAsync(p => p.Code == perm.Code))
            {
                await context.Permissions.AddAsync(perm);
            }
=======
        var defaultPermissions = new[]
        {
            new Permission { Code = "CLASS_VIEW", Name = "View classes" },
            new Permission { Code = "CLASS_CREATE", Name = "Create classes" },
            new Permission { Code = "CLASS_EDIT", Name = "Edit classes" },
            new Permission { Code = "CLASS_DELETE", Name = "Delete classes" },
            new Permission { Code = "MEMBER_VIEW", Name = "View members" },
            new Permission { Code = "MEMBER_CREATE", Name = "Create members" },
            new Permission { Code = "MEMBER_EDIT", Name = "Edit members" },
            new Permission { Code = "MEMBER_DELETE", Name = "Delete members" },
            new Permission { Code = "ROLE_VIEW", Name = "View roles and permissions" },
            new Permission { Code = "ROLE_MANAGE", Name = "Manage roles and permissions" },
            new Permission { Code = "PAYMENT_VIEW", Name = "View payments" },
            new Permission { Code = "PAYMENT_PROCESS", Name = "Process payments" },
            new Permission { Code = "REPORT_VIEW", Name = "View reports" }
        };
        foreach (var permission in defaultPermissions)
        {
            if (!await context.Permissions.AnyAsync(x => x.Code == permission.Code))
                await context.Permissions.AddAsync(permission);
        }
        await context.SaveChangesAsync();

        var administratorRoles = await context.Roles
            .Where(x => x.Name == "Admin" || x.Name == "Manager").ToListAsync();
        var allPermissions = await context.Permissions.ToListAsync();
        foreach (var role in administratorRoles)
        foreach (var permission in allPermissions)
        {
            if (!await context.RolePermissions.AnyAsync(x =>
                    x.RoleId == role.Id && x.PermissionId == permission.Id))
                context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        await context.SaveChangesAsync();

        var memberRole = await context.Roles.SingleAsync(role => role.Name == "Member");
        var user = await context.Users.FirstOrDefaultAsync(
            item => item.Username == "member01" || item.Email == "member01@example.com");
        if (user is null)
        {
            user = new User
            {
                RoleId = memberRole.Id,
                Username = "member01",
                Email = "member01@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Phone = "0912345678",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }
        else if (!user.PasswordHash.StartsWith("$2", StringComparison.Ordinal))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
            user.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        if (!await context.MemberProfiles.AnyAsync(profile => profile.UserId == user.Id))
        {
            await context.MemberProfiles.AddAsync(new MemberProfile
            {
                UserId = user.Id,
                MemberCode = "MB00001",
                FullName = "Nguyễn Văn A",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
>>>>>>> Stashed changes
        }
<<<<<<< Updated upstream

        var seededUser = await context.Users.SingleOrDefaultAsync(user => user.Username == "member01");
        if (seededUser is not null && !seededUser.PasswordHash.StartsWith("$2", StringComparison.Ordinal))
        {
            seededUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
            seededUser.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
=======
        await context.SaveChangesAsync();

        // 3.2. Gán quyền mặc định cho các Role (Admin, Manager)
        var allPermissions = await context.Permissions.ToListAsync();
        var adminRoleObj = await context.Roles.FirstAsync(r => r.Name == "Admin");
        var managerRoleObj = await context.Roles.FirstAsync(r => r.Name == "Manager");

        foreach (var perm in allPermissions)
        {
            if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == adminRoleObj.Id && rp.PermissionId == perm.Id))
            {
                await context.RolePermissions.AddAsync(new RolePermission { RoleId = adminRoleObj.Id, PermissionId = perm.Id });
            }

            if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == managerRoleObj.Id && rp.PermissionId == perm.Id))
            {
                await context.RolePermissions.AddAsync(new RolePermission { RoleId = managerRoleObj.Id, PermissionId = perm.Id });
            }
        }
        await context.SaveChangesAsync();

        var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
        var receptionistRole = await context.Roles.FirstAsync(r => r.Name == "Receptionist");
        var memberRole = await context.Roles.FirstAsync(r => r.Name == "Member");

        var adminUser = await EnsureSeedUserAsync(context, adminRole.Id, "admin", "admin@sportscenter.vn", "Admin@123456", "0900000001");
        await EnsureStaffProfileAsync(context, adminUser, "ST00001", "Quản Trị Viên", "Admin", center.Id);

        var receptionUser = await EnsureSeedUserAsync(context, receptionistRole.Id, "reception01", "reception01@sportscenter.vn", "Reception@123456", "0900000002");
        await EnsureStaffProfileAsync(context, receptionUser, "ST00002", "Nhân Viên Lễ Tân 01", "Receptionist", center.Id);

        var memberUser = await EnsureSeedUserAsync(
            context, memberRole.Id, "member01", "member@scms.vn", "password123", "0912345678");
        await EnsureMemberProfileAsync(context, memberUser, "MB00001");

        var alternateMember = await EnsureSeedUserAsync(
            context, memberRole.Id, "member02", "member01@example.com", "Member@123456", "0912345679");
        await EnsureMemberProfileAsync(context, alternateMember, "MB00002");
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
=======
    }

    private static async Task<User> EnsureSeedUserAsync(
        SportsCenterDbContext context,
        long roleId,
        string username,
        string email,
        string password,
        string phone)
    {
        var user = await context.Users.FirstOrDefaultAsync(
            item => item.Username == username || item.Email == email);
        if (user is null)
        {
            user = new User
            {
                RoleId = roleId,
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Phone = phone,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }
        else if (!user.PasswordHash.StartsWith("$2", StringComparison.Ordinal))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return user;
    }

    private static async Task EnsureMemberProfileAsync(
        SportsCenterDbContext context,
        User user,
        string memberCode)
    {
        if (await context.MemberProfiles.AnyAsync(profile => profile.UserId == user.Id))
        {
            return;
        }

        await context.MemberProfiles.AddAsync(new MemberProfile
        {
            UserId = user.Id,
            MemberCode = memberCode,
            FullName = "Nguyễn Văn A",
            Gender = "Nam",
            DateOfBirth = new DateOnly(1995, 5, 20),
            Address = "Quận 1, TP.HCM",
            FitnessGoal = "Tăng cơ, giảm mỡ, cải thiện sức bền",
            FitnessLevel = "Intermediate",
            HeightCm = 175,
            WeightKg = 70,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task EnsureStaffProfileAsync(
        SportsCenterDbContext context,
        User user,
        string staffCode,
        string fullName,
        string position,
        long centerId)
    {
        if (await context.StaffProfiles.AnyAsync(profile => profile.UserId == user.Id))
        {
            return;
        }

        await context.StaffProfiles.AddAsync(new StaffProfile
        {
            UserId = user.Id,
            CenterId = centerId,
            StaffCode = staffCode,
            FullName = fullName,
            Position = position,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
>>>>>>> Stashed changes
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
        if (state == 0) return;

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

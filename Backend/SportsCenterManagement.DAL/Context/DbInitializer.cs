using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>
/// Khởi tạo và đồng bộ dữ liệu mẫu cho Database theo đúng nghiệp vụ hệ thống.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context)
    {
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // 1. Tạo Center mẫu nếu chưa có
        var center = await context.Centers.FirstOrDefaultAsync();
        if (center == null)
        {
            center = new Center
            {
                Name = "Sports Center Quận 1",
                Address = "123 Nguyễn Thị Minh Khai, Quận 1, TP.HCM",
                Phone = "0353716249",
                Email = "center.q1@sportscenter.vn",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Centers.AddAsync(center);
            await context.SaveChangesAsync();
        }

        // 2. Định nghĩa danh sách các Gói tập chuẩn
        var packagesToSync = new List<MembershipPackage>
        {
            new()
            {
                CenterId = center.Id,
                Name = "Gói Gym & Yoga 1 Tháng",
                Description = "Tập luyện không giới hạn trong 30 ngày",
                DurationDays = 30,
                Price = 500000,
                MaxClasses = 1,
                AccessType = "Gym & Yoga",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                CenterId = center.Id,
                Name = "Gói VIP Premium 3 Tháng",
                Description = "Toàn quyền sử dụng dịch vụ và huấn luyện viên trong 90 ngày",
                DurationDays = 90,
                Price = 1200000,
                MaxClasses = 3,
                AccessType = "Toàn quyền VIP",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            },
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

        // 3. Khởi tạo danh sách các vai trò (Roles) chuẩn
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

        // 4. Khởi tạo danh sách các Quyền hạn (Permissions) chuẩn
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

        foreach (var perm in defaultPermissions)
        {
            if (!await context.Permissions.AnyAsync(p => p.Code == perm.Code))
            {
                await context.Permissions.AddAsync(perm);
            }
        }
        await context.SaveChangesAsync();

        // 5. Gán quyền mặc định cho các Role (Admin, Manager)
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

        // 6. Tạo người dùng và hồ sơ mẫu
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
    }
}

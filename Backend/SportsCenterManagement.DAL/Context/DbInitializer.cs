using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>
/// Khởi tạo và đồng bộ dữ liệu mẫu cho Database theo đúng nghiệp vụ hệ thống.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context, bool seedDemoData)
    {
        await context.Database.MigrateAsync();

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
            return;
        }

        var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
        var managerRole = await context.Roles.FirstAsync(r => r.Name == "Manager");
        var receptionistRole = await context.Roles.FirstAsync(r => r.Name == "Receptionist");
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

        // 6. Tạo tài khoản Member mẫu và MemberProfile nếu chưa có
        var memberUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "member01");
        if (memberUser == null)
        {
            memberUser = new User
            {
                RoleId = memberRole.Id,
                Username = "member01",
                Email = "member01@example.com",
                PasswordHash = HashPassword("Member@123456"),
                Phone = "0912345678",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(memberUser);
            await context.SaveChangesAsync();

            var memberProfile = new MemberProfile
            {
                UserId = memberUser.Id,
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
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32
        );
        return $"{Convert.ToBase64String(salt)}:100000:{Convert.ToBase64String(hash)}";
    }
}

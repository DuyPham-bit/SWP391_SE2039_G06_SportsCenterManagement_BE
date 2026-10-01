using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>
/// Khởi tạo và đồng bộ dữ liệu mẫu cho Database theo đúng nghiệp vụ hệ thống.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context, bool baselineLegacySchema = false)
    {
        if (baselineLegacySchema)
        {
            await BaselineLegacySchemaAsync(context);
        }

        await context.Database.MigrateAsync();

        // 1. Tạo Center mẫu nếu chưa có
        var center = await context.Centers.FirstOrDefaultAsync();
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

        // 2. Định nghĩa danh sách 4 Gói tập chuẩn từ giao diện Frontend
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

                var user = new User
                {
                    RoleId = role.Id,
                    Username = "member01",
                    Email = "member01@example.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEJ...", // hash test
                    Phone = "0912345678",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Users.AddAsync(user);
                await context.SaveChangesAsync();

                var memberProfile = new MemberProfile
                {
                    UserId = user.Id,
                    MemberCode = "MB00001",
                    FullName = "Nguyễn Văn A",
                    CreatedAt = DateTime.UtcNow
                };
                await context.MemberProfiles.AddAsync(memberProfile);
                await context.SaveChangesAsync();
            }
        }
    }
}

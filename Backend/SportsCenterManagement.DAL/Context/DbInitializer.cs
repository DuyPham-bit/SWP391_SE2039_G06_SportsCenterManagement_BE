using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

/// <summary>
/// Khởi tạo dữ liệu mẫu cho Database nếu bảng đang trống để phục vụ test tính năng.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context)
    {
        await context.Database.MigrateAsync();

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

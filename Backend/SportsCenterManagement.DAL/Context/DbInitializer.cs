using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Context;

public static class DbInitializer
{
    public static async Task SeedAsync(SportsCenterDbContext context)
    {
        await context.Database.MigrateAsync();

        if (!await context.Centers.AnyAsync())
        {
            await context.Centers.AddAsync(new Center
            {
                Name = "Sports Center Quận 1",
                Address = "123 Nguyễn Thị Minh Khai, Quận 1, TP.HCM",
                Phone = "0353716249",
                Email = "center.q1@sportscenter.vn",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var center = await context.Centers.OrderBy(item => item.Id).FirstAsync();
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
                    Description = "Toàn quyền sử dụng dịch vụ trong 90 ngày",
                    DurationDays = 90,
                    Price = 1200000,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();
        }

        foreach (var roleName in new[] { "Member", "Manager", "Receptionist" })
        {
            if (await context.Roles.AnyAsync(role => role.Name == roleName))
            {
                continue;
            }

            await context.Roles.AddAsync(new Role
            {
                Name = roleName,
                Description = roleName == "Member" ? "Khách hàng hội viên" : "Nhân sự trung tâm",
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();
    }
}

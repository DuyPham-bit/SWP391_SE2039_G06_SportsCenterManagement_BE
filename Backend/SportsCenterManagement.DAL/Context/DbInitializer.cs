using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
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
                    Email = "member@scms.vn",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
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

        var seededUser = await context.Users.SingleOrDefaultAsync(user => user.Username == "member01");
        if (seededUser is not null && !seededUser.PasswordHash.StartsWith("$2", StringComparison.Ordinal))
        {
            seededUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");
            seededUser.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
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
    }
}

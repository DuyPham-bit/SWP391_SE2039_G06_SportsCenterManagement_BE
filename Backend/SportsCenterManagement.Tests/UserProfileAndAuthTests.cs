using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SportsCenterManagement.API.Controllers;
using SportsCenterManagement.API.DTOs.Auth;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using Xunit;

namespace SportsCenterManagement.Tests;

public class UserProfileAndAuthTests
{
    private static SportsCenterDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SportsCenterDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new SportsCenterDbContext(options);
    }

    private static IConfiguration CreateMockConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "SuperSecretKeyForTestingSportsCenterManagementApp2026!" },
            { "Jwt:Issuer", "SportsCenterManagement" },
            { "Jwt:Audience", "SportsCenterManagementUsers" },
            { "Jwt:ExpiresMinutes", "60" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private static IHostEnvironment CreateMockEnvironment()
    {
        var mockEnv = new MoqHostEnvironment { EnvironmentName = Environments.Development };
        return mockEnv;
    }

    private class MoqHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SportsCenterManagement.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static void ControllerContextWithUser(ControllerBase controller, long userId, string email, string role = "MEMBER")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Email, email)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task Test_Login_WithValidCredentials_ReturnsToken()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await DbInitializer.SeedAsync(db);

        var authController = new AuthController(db, CreateMockConfiguration(), CreateMockEnvironment());

        // 1. Đăng nhập đúng mật khẩu
        var loginResult = await authController.Login(
            new LoginRequest { Email = "member@scms.vn", Password = "password123" },
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(loginResult.Result);
        var authResponse = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.NotNull(authResponse.AccessToken);
        Assert.Equal("MEMBER", authResponse.Role);

        // 2. Đăng nhập sai mật khẩu
        var invalidLogin = await authController.Login(
            new LoginRequest { Email = "member@scms.vn", Password = "WrongPassword123!" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(invalidLogin.Result);
    }

    [Fact]
    public async Task Test_GetProfile_ReturnsAuthenticatedUserProfile()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await DbInitializer.SeedAsync(db);

        var memberUser = await db.Users.FirstAsync(u => u.Email == "member@scms.vn");
        var profileController = new ProfileController(db);
        ControllerContextWithUser(profileController, memberUser.Id, memberUser.Email, "MEMBER");

        var response = await profileController.GetMe(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var profile = Assert.IsType<ProfileResponse>(okResult.Value);

        Assert.Equal(memberUser.Id, profile.UserId);
        Assert.Equal("member@scms.vn", profile.Email);
        Assert.Equal("Nguyễn Văn A", profile.FullName);
        Assert.Equal("0912345678", profile.Phone);
        Assert.Equal("MB00001", profile.MemberCode);
    }

    [Fact]
    public async Task Test_UpdateProfile_UpdatesFullNameAndPhone()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await DbInitializer.SeedAsync(db);

        var memberUser = await db.Users.FirstAsync(u => u.Email == "member@scms.vn");
        var profileController = new ProfileController(db);
        ControllerContextWithUser(profileController, memberUser.Id, memberUser.Email, "MEMBER");

        var updateRequest = new UpdateProfileRequest
        {
            FullName = "Nguyễn Văn B",
            Phone = "0987654321"
        };

        var response = await profileController.UpdateMe(updateRequest, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var profile = Assert.IsType<ProfileResponse>(okResult.Value);

        Assert.Equal("Nguyễn Văn B", profile.FullName);
        Assert.Equal("0987654321", profile.Phone);

        // Đảm bảo dữ liệu đã lưu vào DB
        var memberProfileInDb = await db.MemberProfiles.FirstAsync(m => m.UserId == memberUser.Id);
        Assert.Equal("Nguyễn Văn B", memberProfileInDb.FullName);

        var userInDb = await db.Users.FirstAsync(u => u.Id == memberUser.Id);
        Assert.Equal("0987654321", userInDb.Phone);
    }

    [Fact]
    public async Task Test_ChangePassword_And_LoginWithNewPassword_And_VerifyDatabaseHash()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await DbInitializer.SeedAsync(db);

        var user = await db.Users.FirstAsync(u => u.Email == "member@scms.vn");
        var authController = new AuthController(db, CreateMockConfiguration(), CreateMockEnvironment());
        ControllerContextWithUser(authController, user.Id, user.Email, "MEMBER");

        var newPassword = "NewSecretPassword@123";

        // 1. Thực hiện đổi mật khẩu
        var changeResult = await authController.ChangePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "password123",
                NewPassword = newPassword
            },
            CancellationToken.None);

        var okChangeResult = Assert.IsType<OkObjectResult>(changeResult.Result);
        var changeAuthResponse = Assert.IsType<AuthResponse>(okChangeResult.Value);
        Assert.NotNull(changeAuthResponse.AccessToken);

        // 2. KIỂM TRA MẬT KHẨU TRONG DATABASE ĐÃ ĐƯỢC HASH CHƯA
        var updatedUserInDb = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.NotNull(updatedUserInDb.PasswordHash);
        // Mật khẩu KHÔNG ĐƯỢC lưu dưới dạng plain text "NewSecretPassword@123"
        Assert.NotEqual(newPassword, updatedUserInDb.PasswordHash);
        // Kiểm tra tiền tố BCrypt ($2a$, $2b$, ...)
        Assert.StartsWith("$2", updatedUserInDb.PasswordHash);
        // Xác minh hash BCrypt khớp với mật khẩu mới
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, updatedUserInDb.PasswordHash));
        // Xác minh hash BCrypt KHÔNG khớp với mật khẩu cũ
        Assert.False(BCrypt.Net.BCrypt.Verify("password123", updatedUserInDb.PasswordHash));

        // 3. ĐĂNG NHẬP LẠI BẰNG MẬT KHẨU MỚI
        var newLoginResult = await authController.Login(
            new LoginRequest { Email = "member@scms.vn", Password = newPassword },
            CancellationToken.None);

        var okLogin = Assert.IsType<OkObjectResult>(newLoginResult.Result);
        var newAuth = Assert.IsType<AuthResponse>(okLogin.Value);
        Assert.NotNull(newAuth.AccessToken);

        // 4. Thử đăng nhập lại bằng mật khẩu CŨ -> Phải thất bại 401
        var oldLoginResult = await authController.Login(
            new LoginRequest { Email = "member@scms.vn", Password = "password123" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(oldLoginResult.Result);
    }

    [Fact]
    public async Task Test_UpdateProfile_WhenNoProfileExists_CreatesProfileRecord()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await DbInitializer.SeedAsync(db);

        var role = await db.Roles.FirstAsync(r => r.Name == "Admin");
        var newUser = new User
        {
            RoleId = role.Id,
            Username = "newadmin",
            Email = "newadmin@sportscenter.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.AddAsync(newUser);
        await db.SaveChangesAsync();

        var profileController = new ProfileController(db);
        ControllerContextWithUser(profileController, newUser.Id, newUser.Email, "ADMIN");

        var updateRequest = new UpdateProfileRequest
        {
            FullName = "Quản Trị Mới",
            Phone = "0933445566"
        };

        var response = await profileController.UpdateMe(updateRequest, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var profile = Assert.IsType<ProfileResponse>(okResult.Value);

        Assert.Equal("Quản Trị Mới", profile.FullName);
        Assert.Equal("0933445566", profile.Phone);

        var staffProfileInDb = await db.StaffProfiles.FirstOrDefaultAsync(s => s.UserId == newUser.Id);
        Assert.NotNull(staffProfileInDb);
        Assert.Equal("Quản Trị Mới", staffProfileInDb.FullName);
    }
}

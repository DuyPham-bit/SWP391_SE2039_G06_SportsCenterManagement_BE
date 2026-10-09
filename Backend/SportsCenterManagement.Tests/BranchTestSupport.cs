using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SportsCenterManagement.API.DTOs.Auth;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using Xunit;

namespace SportsCenterManagement.Tests;

public sealed class LoginIntegrationTests(BranchApiFactory factory) : IClassFixture<BranchApiFactory>
{
    [Fact]
    public async Task ExistingLoginReturnsRealJwt()
    {
        using var client = factory.CreateClient();
        var auth = await BranchApiFactory.LoginAsync(client, "member@test.local");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);
        Assert.Equal("10", jwt.Subject);
        Assert.Equal(10, auth.UserId);
        Assert.Equal("MEMBER", auth.Role);
        Assert.True(auth.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(58));
    }

    [Fact]
    public async Task WrongPasswordDoesNotIssueToken()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "member@test.local", password = "WrongPassword" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class BranchApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test@Password123!";
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public BranchApiFactory()
    {
        connection.Open();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
        db.Database.EnsureCreated();
        BranchTestData.Seed(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../SportsCenterManagement.API")));
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Key", "test-only-branch-integration-jwt-key-at-least-32-bytes");
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Error));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<SportsCenterDbContext>();
            services.RemoveAll<DbContextOptions<SportsCenterDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SportsCenterDbContext>>();
            services.AddDbContext<SportsCenterDbContext>(options => options.UseSqlite(connection)
                .ReplaceService<IModelCustomizer, BranchSqliteModelCustomizer>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    public static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }
}

internal sealed class BranchSqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            property.SetColumnType(null);
        }
        // SQLite has no SQL Server Unicode string prefix in filtered index expressions.
        foreach (var index in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetIndexes()))
        {
            var filter = index.GetFilter();
            if (filter is not null) index.SetFilter(filter.Replace("N'", "'", StringComparison.Ordinal));
        }
    }
}

internal static class BranchTestData
{
    public static SportsCenterDbContext CreateDatabase()
    {
        var db = new SportsCenterDbContext(new DbContextOptionsBuilder<SportsCenterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        Seed(db);
        return db;
    }

    public static void Seed(SportsCenterDbContext db)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(BranchApiFactory.Password, workFactor: 4);
        db.Centers.AddRange(new Center { Id = 1, Name = "Center 1", Address = "Address 1", Status = "Active" },
            new Center { Id = 2, Name = "Center 2", Address = "Address 2", Status = "Active" });
        db.Roles.AddRange(new Role { Id = 1, Name = "Admin" }, new Role { Id = 2, Name = "Manager" },
            new Role { Id = 3, Name = "Receptionist" }, new Role { Id = 4, Name = "Member" });
        User Account(long id, long roleId, string name) => new()
        {
            Id = id, RoleId = roleId, Username = name, Email = name + "@test.local", PasswordHash = hash, Status = "Active"
        };
        db.Users.AddRange(Account(10, 4, "member"), Account(20, 3, "reception"),
            Account(30, 2, "manager"), Account(40, 3, "other-reception"), Account(100, 1, "admin"));
        StaffProfile Staff(long id, long userId, long centerId) => new()
        {
            Id = id, UserId = userId, CenterId = centerId, StaffCode = "ST" + id,
            FullName = "Staff", Position = "Staff", Status = "Active"
        };
        db.StaffProfiles.AddRange(Staff(1, 20, 1), Staff(2, 30, 1), Staff(3, 40, 2));
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, MemberCode = "MB001", FullName = "Member" });
        db.MembershipPackages.AddRange(
            new MembershipPackage { Id = 1, CenterId = 1, Name = "Monthly", DurationDays = 30, Price = 100m, Status = "Active" },
            new MembershipPackage { Id = 2, CenterId = 2, Name = "Other Center", DurationDays = 30, Price = 100m, Status = "Active" });
        var today = VietnamTime.GetDate(DateTime.UtcNow);
        db.MemberSubscriptions.Add(new MemberSubscription
        {
            Id = 1, MemberId = 1, PackageId = 1, StartDate = today.AddDays(-1), EndDate = today.AddDays(30),
            Price = 100m, Status = "Active", CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

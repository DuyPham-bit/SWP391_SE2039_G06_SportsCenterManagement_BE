using SportsCenterManagement.BLL.Services.Profile;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Auth;

namespace SportsCenterManagement.Tests.BLL.Profile;

public class ProfileServiceTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsCenterNameAndStaffCode_ForReceptionist()
    {
        using var db = TestDb.Create();
        var role = new Role { Id = 4, Name = "Receptionist" };
        var center = new Center { Id = 1, Name = "PARSE CENTER", Address = "1/11 Hung Vuong", Status = "Active", CreatedAt = DateTime.UtcNow };
        var user = new User
        {
            Id = 14,
            Username = "lan.dm",
            Email = "lan.dm@example.com",
            PasswordHash = "hash",
            RoleId = role.Id,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var staff = new StaffProfile
        {
            Id = 5,
            UserId = user.Id,
            CenterId = center.Id,
            StaffCode = "ST0D82DE757D",
            FullName = "Trần Ngọc Lan",
            Position = "Receptionist",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Roles.Add(role);
        db.Centers.Add(center);
        db.Users.Add(user);
        db.StaffProfiles.Add(staff);
        await db.SaveChangesAsync();

        var service = new ProfileService(new UserRepository(db), new UnitOfWork(db));
        var profile = await service.GetProfileAsync(user.Id);

        Assert.NotNull(profile);
        Assert.Equal("Trần Ngọc Lan", profile.FullName);
        Assert.Equal(1, profile.CenterId);
        Assert.Equal("PARSE CENTER", profile.CenterName);
        Assert.Equal("ST0D82DE757D", profile.StaffCode);
    }
}

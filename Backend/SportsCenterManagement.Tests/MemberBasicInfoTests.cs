using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MembersRequests = SportsCenterManagement.BLL.DTOs.Members.Requests;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

namespace SportsCenterManagement.Tests;

public class MemberBasicInfoTests
{
    [Fact]
    public async Task Receptionist_UpdatesBasicInfo_WithoutTouchingLoginPhone_AndIsAudited()
    {
        using var db = TestDb.Create();
        var (receptionist, member, memberUser) = Seed(db);

        var result = await Service(db).UpdateBasicInfoAtCenterAsync(receptionist.Id, 1, member.Id,
            new MembersRequests.UpdateMemberBasicInfoRequest("Nguyễn Văn B", new DateOnly(1995, 5, 1), "Male", "Mẹ", "0911222333"));

        Assert.Equal("Nguyễn Văn B", result.FullName);
        Assert.Equal("0911222333", result.EmergencyContactPhone);
        db.ChangeTracker.Clear();
        Assert.Equal("0900000001", (await db.Users.SingleAsync(u => u.Id == memberUser.Id)).Phone);
        Assert.Equal("member.basic_info.updated.by_counter", (await db.AuditLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task MemberOfAnotherCenter_IsNotFound()
    {
        using var db = TestDb.Create();
        var (receptionist, member, _) = Seed(db);
        member.CenterId = 2;
        db.SaveChanges();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).UpdateBasicInfoAtCenterAsync(receptionist.Id, 1, member.Id,
            new MembersRequests.UpdateMemberBasicInfoRequest("X", null, null, null, null)));
    }

    private static MemberService Service(SportsCenterDbContext db) =>
        new(new UnitOfWork(db), null!, new BusinessClock(new ConfigurationBuilder().Build()));

    private static (User Receptionist, MemberProfile Member, User MemberUser) Seed(SportsCenterDbContext db)
    {
        db.AddRange(
            new Center { Id = 1, Name = "C1", Address = "A", Status = "Active", CreatedAt = TestDb.Now },
            new Center { Id = 2, Name = "C2", Address = "B", Status = "Active", CreatedAt = TestDb.Now },
            new Role { Id = 10, Name = "Receptionist" },
            new Role { Id = 11, Name = "Member" });
        var receptionist = new User { Username = "desk", Email = "desk@example.test", PasswordHash = "x", RoleId = 10, Status = "Active", CreatedAt = TestDb.Now };
        var memberUser = new User { Username = "m1", Email = "m1@example.test", Phone = "0900000001", PasswordHash = "x", RoleId = 11, Status = "Active", CreatedAt = TestDb.Now };
        db.AddRange(receptionist, memberUser);
        db.SaveChanges();
        db.Add(new StaffProfile { UserId = receptionist.Id, CenterId = 1, StaffCode = "R1", FullName = "Desk", Position = "Receptionist", Status = "Active", CreatedAt = TestDb.Now });
        var member = new MemberProfile { UserId = memberUser.Id, CenterId = 1, MemberCode = "M1", FullName = "Nguyen Van A", CreatedAt = TestDb.Now };
        db.Add(member);
        db.SaveChanges();
        return (receptionist, member, memberUser);
    }
}

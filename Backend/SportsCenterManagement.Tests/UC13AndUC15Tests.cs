using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsCenterManagement.API.Controllers;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.DTOs.Roles;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;
using Xunit;

namespace SportsCenterManagement.Tests;

public class UC13AndUC15Tests
{
    private static SportsCenterDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SportsCenterDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SportsCenterDbContext(options);
    }

    private static async Task SeedTestDataAsync(SportsCenterDbContext db)
    {
        await DbInitializer.SeedAsync(db);

        var roleNames = new[] { "Admin", "Super Admin", "Manager", "Receptionist", "Coach", "Member" };
        foreach (var roleName in roleNames)
        {
            if (!await db.Roles.AnyAsync(role => role.Name == roleName))
            {
                db.Roles.Add(new Role { Name = roleName, Description = $"System role: {roleName}", CreatedAt = DateTime.UtcNow });
            }
        }

        var permissionDefinitions = new Dictionary<string, (string Name, string Description)>
        {
            ["CLASS_VIEW"] = ("View classes", "View class data"),
            ["CLASS_MANAGE"] = ("Manage classes", "Create and update classes"),
            ["COACH_ASSIGN"] = ("Assign coaches", "Assign coaches to classes"),
            ["MEMBER_VIEW"] = ("View members", "View member data"),
            ["MEMBER_MANAGE"] = ("Manage members", "Create and update members"),
            ["ROLE_MANAGE"] = ("Manage roles", "Manage roles and permissions")
        };

        foreach (var (code, definition) in permissionDefinitions)
        {
            if (!await db.Permissions.AnyAsync(permission => permission.Code == code))
            {
                db.Permissions.Add(new Permission
                {
                    Code = code,
                    Name = definition.Name,
                    Description = definition.Description
                });
            }
        }

        await db.SaveChangesAsync();

        var adminRole = await db.Roles.SingleAsync(role => role.Name == "Admin");
        if (!await db.Users.AnyAsync(user => user.Email == "admin@sportscenter.vn"))
        {
            db.Users.Add(new User
            {
                RoleId = adminRole.Id,
                Username = "admin",
                Email = "admin@sportscenter.vn",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static void ControllerContextWithUser(ControllerBase controller, long userId, string role = "Admin")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task Test_AssignCoachToClass_Success_WhenNoOverlap()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);

        var center = await db.Centers.FirstAsync();

        var sport = new Sport { Name = "Bơi Lội", Description = "Water sports", Status = "Active" };
        await db.Sports.AddAsync(sport);
        await db.SaveChangesAsync();

        var classEntity = new ClassEntity
        {
            CenterId = center.Id,
            SportId = sport.Id,
            Name = "Lớp Bơi Căn Bản",
            Capacity = 20,
            DurationMinutes = 60,
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };
        await db.Classes.AddAsync(classEntity);
        await db.SaveChangesAsync();
        await db.ClassSchedules.AddAsync(new ClassSchedule
        {
            ClassId = classEntity.Id,
            DayOfWeek = (int)DateTime.UtcNow.DayOfWeek,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0),
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var coachUser = new User
        {
            RoleId = (await db.Roles.FirstAsync(r => r.Name == "Coach")).Id,
            Username = "coach_swim",
            Email = "coach_swim@scms.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CoachPass@123"),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.AddAsync(coachUser);
        await db.SaveChangesAsync();

        var coachProfile = new CoachProfile
        {
            UserId = coachUser.Id,
            CenterId = center.Id,
            CoachCode = "CH00001",
            FullName = "Nguyễn Văn HLV",
            Specialization = "Bơi Lội",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.CoachProfiles.AddAsync(coachProfile);
        await db.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(db);
        var classService = new ClassService(unitOfWork);
        var controller = new ClassesController(classService);

        var assignRequest = new AssignCoachRequest { CoachId = coachProfile.Id, IsPrimary = true };
        var result = await controller.AssignCoach(classEntity.Id, assignRequest, CancellationToken.None);

        var okResult = result.Result as OkObjectResult;
        Assert.True(okResult is not null, (result.Result as BadRequestObjectResult)?.Value?.ToString());
        var coachResponse = Assert.IsType<ClassCoachResponse>(okResult.Value);

        Assert.Equal(classEntity.Id, coachResponse.ClassId);
        Assert.Equal(coachProfile.Id, coachResponse.CoachId);
        Assert.Equal("Nguyễn Văn HLV", coachResponse.CoachName);
        Assert.True(coachResponse.IsPrimary);

        var classCoachInDb = await db.ClassCoaches.SingleOrDefaultAsync(cc => cc.ClassId == classEntity.Id && cc.CoachId == coachProfile.Id);
        Assert.NotNull(classCoachInDb);

        var secondCoachUser = new User
        {
            RoleId = coachUser.RoleId,
            Username = "coach_swim_2",
            Email = "coach_swim_2@scms.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CoachPass@123"),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(secondCoachUser);
        await db.SaveChangesAsync();
        var secondCoach = new CoachProfile
        {
            UserId = secondCoachUser.Id,
            CenterId = center.Id,
            CoachCode = "CH00002",
            FullName = "HLV Bơi thứ hai",
            Specialization = "Bơi Lội",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.CoachProfiles.Add(secondCoach);
        await db.SaveChangesAsync();

        var duplicateCoachAssignment = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            classService.AssignCoachToClassAsync(classEntity.Id, new AssignCoachRequest { CoachId = secondCoach.Id, IsPrimary = true }));
        Assert.Contains("đã có HLV được phân công", duplicateCoachAssignment.Message);
        Assert.Equal(1, await db.ClassCoaches.CountAsync(cc => cc.ClassId == classEntity.Id && cc.IsPrimary));
        Assert.Single(await db.ClassCoaches.Where(cc => cc.ClassId == classEntity.Id).ToListAsync());
    }

    [Fact]
    public async Task Test_AssignCoachToClass_Fails_WhenScheduleOverlaps()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);

        var center = await db.Centers.FirstAsync();
        var sport = new Sport { Name = "Gym", Description = "Fitness", Status = "Active" };
        await db.Sports.AddAsync(sport);
        await db.SaveChangesAsync();

        // 1. Tạo Lớp 1 có lịch Thứ 2 (DayOfWeek=1), 08:00 - 10:00
        var class1 = new ClassEntity
        {
            CenterId = center.Id,
            SportId = sport.Id,
            Name = "Lớp Gym Sáng",
            Capacity = 15,
            DurationMinutes = 120,
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };
        await db.Classes.AddAsync(class1);
        await db.SaveChangesAsync();

        await db.ClassSchedules.AddAsync(new ClassSchedule
        {
            ClassId = class1.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Status = "Active"
        });
        await db.SaveChangesAsync();

        // 2. Tạo Lớp 2 có lịch Thứ 2 (DayOfWeek=1), 09:00 - 11:00 (Trùng 09:00-10:00 với Lớp 1)
        var class2 = new ClassEntity
        {
            CenterId = center.Id,
            SportId = sport.Id,
            Name = "Lớp Gym Trưa",
            Capacity = 15,
            DurationMinutes = 120,
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };
        await db.Classes.AddAsync(class2);
        await db.SaveChangesAsync();

        await db.ClassSchedules.AddAsync(new ClassSchedule
        {
            ClassId = class2.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(11, 0),
            Status = "Active"
        });
        await db.SaveChangesAsync();

        // 3. Tạo HLV Gym
        var coachUser = new User
        {
            RoleId = (await db.Roles.FirstAsync(r => r.Name == "Coach")).Id,
            Username = "coach_gym",
            Email = "coach_gym@scms.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CoachPass@123"),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.AddAsync(coachUser);
        await db.SaveChangesAsync();

        var coachProfile = new CoachProfile
        {
            UserId = coachUser.Id,
            CenterId = center.Id,
            CoachCode = "CH00002",
            FullName = "Trần Văn Gym",
            Specialization = "Gym",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.CoachProfiles.AddAsync(coachProfile);
        await db.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(db);
        var classService = new ClassService(unitOfWork);

        // 4. Phân công HLV vào Lớp 1 -> Thành công
        await classService.AssignCoachToClassAsync(class1.Id, new AssignCoachRequest { CoachId = coachProfile.Id }, CancellationToken.None);

        var longDayClass = new ClassEntity
        {
            CenterId = center.Id,
            SportId = sport.Id,
            Name = "Lớp Gym Chiều Dài",
            Capacity = 15,
            DurationMinutes = 420,
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };
        db.Classes.Add(longDayClass);
        await db.SaveChangesAsync();
        db.ClassSchedules.Add(new ClassSchedule
        {
            ClassId = longDayClass.Id,
            DayOfWeek = 1,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var dailyLimitException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            classService.AssignCoachToClassAsync(longDayClass.Id, new AssignCoachRequest { CoachId = coachProfile.Id }, CancellationToken.None));
        Assert.Contains("8.0 giờ/ngày", dailyLimitException.Message);

        // 5. Thử phân công HLV đó vào Lớp 2 -> Phải bắt được ngoại lệ trùng lịch dạy
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            classService.AssignCoachToClassAsync(class2.Id, new AssignCoachRequest { CoachId = coachProfile.Id }, CancellationToken.None));

        Assert.Contains("trùng lịch dạy", ex.Message);
        Assert.Contains("Gym Sáng", ex.Message);
    }

    [Fact]
    public async Task Test_AssignCoachToClass_Fails_WhenSpecializationMismatch()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);

        var center = await db.Centers.FirstAsync();
        var sportSwim = new Sport { Name = "Bơi Lội", Description = "Swimming", Status = "Active" };
        await db.Sports.AddAsync(sportSwim);
        await db.SaveChangesAsync();

        var swimClass = new ClassEntity
        {
            CenterId = center.Id,
            SportId = sportSwim.Id,
            Name = "Lớp Bơi Nâng Cao",
            Capacity = 10,
            DurationMinutes = 60,
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };
        await db.Classes.AddAsync(swimClass);
        await db.SaveChangesAsync();

        // HLV có chuyên môn Gym cơ bản (Không phải Bơi Lội)
        var coachUser = new User
        {
            RoleId = (await db.Roles.FirstAsync(r => r.Name == "Coach")).Id,
            Username = "coach_gym_only",
            Email = "coach_gym_only@scms.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CoachPass@123"),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.AddAsync(coachUser);
        await db.SaveChangesAsync();

        var coachProfile = new CoachProfile
        {
            UserId = coachUser.Id,
            CenterId = center.Id,
            CoachCode = "CH00088",
            FullName = "Phạm Văn Gym Cơ Bản",
            Specialization = "Gym Cơ Bản",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.CoachProfiles.AddAsync(coachProfile);
        await db.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(db);
        var classService = new ClassService(unitOfWork);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            classService.AssignCoachToClassAsync(swimClass.Id, new AssignCoachRequest { CoachId = coachProfile.Id }, CancellationToken.None));

        Assert.Contains("không có chứng chỉ/chuyên môn phù hợp", ex.Message);
    }

    [Fact]
    public async Task Test_AssignCoachToClass_Fails_WhenClassCancelledOrCoachSuspended()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);

        var center = await db.Centers.FirstAsync();
        var sport = new Sport { Name = "Tennis", Description = "Tennis", Status = "Active" };
        await db.Sports.AddAsync(sport);
        await db.SaveChangesAsync();

        var cancelledClass = new ClassEntity
        {
            CenterId = center.Id,
            SportId = sport.Id,
            Name = "Lớp Tennis Đã Hủy",
            Capacity = 10,
            DurationMinutes = 60,
            Status = "Cancelled",
            CreatedAt = DateTime.UtcNow
        };
        await db.Classes.AddAsync(cancelledClass);
        await db.SaveChangesAsync();

        var suspendedCoachUser = new User
        {
            RoleId = (await db.Roles.FirstAsync(r => r.Name == "Coach")).Id,
            Username = "coach_suspended",
            Email = "coach_suspended@scms.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CoachPass@123"),
            Status = "Inactive",
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.AddAsync(suspendedCoachUser);
        await db.SaveChangesAsync();

        var coachProfile = new CoachProfile
        {
            UserId = suspendedCoachUser.Id,
            CenterId = center.Id,
            CoachCode = "CH00099",
            FullName = "Đỗ Văn Đình Chỉ",
            Specialization = "Tennis",
            Status = "Suspended",
            CreatedAt = DateTime.UtcNow
        };
        await db.CoachProfiles.AddAsync(coachProfile);
        await db.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(db);
        var classService = new ClassService(unitOfWork);

        // 1. Gán vào lớp đã bị hủy -> Thất bại
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            classService.AssignCoachToClassAsync(cancelledClass.Id, new AssignCoachRequest { CoachId = coachProfile.Id }, CancellationToken.None));
        Assert.Contains("ở trạng thái 'Cancelled'", ex1.Message);

        // 2. Đổi lớp sang Active nhưng HLV bị Suspended -> Thất bại
        cancelledClass.Status = "Published";
        await db.SaveChangesAsync();

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            classService.AssignCoachToClassAsync(cancelledClass.Id, new AssignCoachRequest { CoachId = coachProfile.Id }, CancellationToken.None));
        Assert.Contains("không ở trạng thái hoạt động", ex2.Message);
    }

    [Fact]
    public async Task Test_UC15_CreateRole_Fails_WhenDuplicateName()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);

        var unitOfWork = new UnitOfWork(db);
        var roleService = new RolePermissionService(unitOfWork);

        // Thử tạo vai trò trùng tên "Admin" (viết hoa/thường)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roleService.CreateRoleAsync(new CreateRoleRequest { Name = "admin" }, CancellationToken.None));

        Assert.Contains("đã tồn tại trong hệ thống", ex.Message);
    }

    [Fact]
    public async Task Test_UC15_RejectsBlankRoleName_EmptyPermissions_AndSystemRolePermissionChanges()
    {
        Assert.NotEmpty(typeof(RolesController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);
        var service = new RolePermissionService(new UnitOfWork(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRoleAsync(new CreateRoleRequest { Name = "   " }));

        var managerRole = await db.Roles.SingleAsync(role => role.Name == "Manager");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRoleAsync(managerRole.Id, new UpdateRoleRequest { Name = "Manager Edited" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync(managerRole.Id));

        var customRole = await service.CreateRoleAsync(new CreateRoleRequest { Name = "CustomRole" });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRoleAsync(customRole.Id, new UpdateRoleRequest { Name = "   " }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRolePermissionsAsync(customRole.Id, new UpdateRolePermissionsRequest { PermissionIds = [] }));

        var permission = await db.Permissions.FirstAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRolePermissionsAsync(managerRole.Id, new UpdateRolePermissionsRequest { PermissionIds = [permission.Id] }));

        var adminRole = await db.Roles.SingleAsync(role => role.Name == "Admin");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRolePermissionsAsync(adminRole.Id, new UpdateRolePermissionsRequest { PermissionIds = [permission.Id] }));
    }

    [Fact]
    public async Task Test_UC15_DeleteRole_Fails_WhenSystemRoleOrUsersAssigned()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);

        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
        var unitOfWork = new UnitOfWork(db);
        var roleService = new RolePermissionService(unitOfWork);

        // 1. Xóa Role hệ thống Admin -> Bị chặn
        var exSystem = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roleService.DeleteRoleAsync(adminRole.Id, CancellationToken.None));
        Assert.Contains("System Roles", exSystem.Message);

        // 2. Tạo Role mới nhưng có gán User -> Bị chặn khi xóa
        var customRole = await roleService.CreateRoleAsync(new CreateRoleRequest { Name = "CustomRole" }, CancellationToken.None);
        var user = new User
        {
            RoleId = customRole.Id,
            Username = "user_custom",
            Email = "custom@scms.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123456"),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        var exInUse = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roleService.DeleteRoleAsync(customRole.Id, CancellationToken.None));
        Assert.Contains("người dùng đang được gán", exInUse.Message);
    }

    [Fact]
    public async Task Test_UC15_UpdateRolePermissions_Fails_OnSelfLockout_And_AutoIncludesParentPermissions()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);

        var unitOfWork = new UnitOfWork(db);
        var roleService = new RolePermissionService(unitOfWork);
        var selfManagedRole = await roleService.CreateRoleAsync(new CreateRoleRequest { Name = "CustomAdmin" });
        var adminUser = new User
        {
            RoleId = selfManagedRole.Id,
            Username = "custom_admin",
            Email = "custom_admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        var controller = new RolesController(roleService);
        ControllerContextWithUser(controller, adminUser.Id, "Admin");

        var allPerms = await roleService.GetAllPermissionsAsync(CancellationToken.None);
        var classAssignPerm = allPerms.First(p => p.Code == "COACH_ASSIGN");

        // 1. Thử bỏ quyền ROLE_MANAGE khỏi vai trò của chính Admin -> Bắt lỗi Self-Lockout
        var updateRequestSelfLockout = new UpdateRolePermissionsRequest
        {
            PermissionIds = [classAssignPerm.Id] // Không có ROLE_MANAGE
        };

        var exSelfLockout = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roleService.UpdateRolePermissionsAsync(selfManagedRole.Id, updateRequestSelfLockout, adminUser.Id, CancellationToken.None));

        Assert.Contains("ROLE_MANAGE", exSelfLockout.Message);

        // 2. Kiểm tra tự động bổ sung quyền cha CLASS_VIEW khi gán quyền con COACH_ASSIGN cho Role khác
        var managerRole = await roleService.CreateRoleAsync(new CreateRoleRequest { Name = "CustomManager" });
        var updateRequestChildOnly = new UpdateRolePermissionsRequest
        {
            PermissionIds = [classAssignPerm.Id] // Chỉ chọn COACH_ASSIGN, không chọn CLASS_VIEW
        };

        var result = await roleService.UpdateRolePermissionsAsync(managerRole.Id, updateRequestChildOnly, null, CancellationToken.None);

        Assert.Contains(result.Permissions, p => p.Code == "COACH_ASSIGN");
        Assert.Contains(result.Permissions, p => p.Code == "CLASS_VIEW"); // Tự động bổ sung quyền cha CLASS_VIEW
    }

    [Fact]
    public async Task Test_UC15_UpdateRole_Fails_WhenNameAlreadyExists()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);
        var service = new RolePermissionService(new UnitOfWork(db));
        var first = await service.CreateRoleAsync(new CreateRoleRequest { Name = "Sales" });
        var second = await service.CreateRoleAsync(new CreateRoleRequest { Name = "Support" });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRoleAsync(second.Id, new UpdateRoleRequest { Name = " sales " }));

        Assert.Contains("đã tồn tại", error.Message);
        Assert.Equal("Support", (await db.Roles.FindAsync(second.Id))!.Name);
    }

    [Fact]
    public async Task Test_UC13_RejectsApprovedLeaveAndASecondCoachForOneSlot()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);
        var center = await db.Centers.FirstAsync();
        var sport = new Sport { Name = "Gym", Status = "Active" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var classEntity = new ClassEntity
        {
            CenterId = center.Id,
            SportId = sport.Id,
            Name = "Lớp UC13 test",
            Capacity = 10,
            DurationMinutes = 60,
            Status = "Published",
            CreatedAt = DateTime.UtcNow
        };
        db.Classes.Add(classEntity);
        await db.SaveChangesAsync();
        db.ClassSchedules.Add(new ClassSchedule
        {
            ClassId = classEntity.Id,
            DayOfWeek = (int)today.DayOfWeek,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            StartDate = today,
            EndDate = today.AddDays(7),
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var coachRoleId = (await db.Roles.SingleAsync(role => role.Name == "Coach")).Id;
        var coachUser = new User
        {
            RoleId = coachRoleId,
            Username = "coach_uc13_test",
            Email = "coach_uc13_test@example.com",
            PasswordHash = "hash",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(coachUser);
        await db.SaveChangesAsync();
        var coach = new CoachProfile
        {
            UserId = coachUser.Id,
            CenterId = center.Id,
            CoachCode = "CHUC1301",
            FullName = "Coach UC13",
            Specialization = "Gym",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.CoachProfiles.Add(coach);
        await db.SaveChangesAsync();
        db.CoachLeaves.Add(new CoachLeave
        {
            CoachId = coach.Id,
            StartAt = today.ToDateTime(new TimeOnly(9, 30)),
            EndAt = today.ToDateTime(new TimeOnly(12, 0)),
            Status = "Approved"
        });
        await db.SaveChangesAsync();

        var service = new ClassService(new UnitOfWork(db));
        var leaveError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignCoachToClassAsync(classEntity.Id, new AssignCoachRequest { CoachId = coach.Id }));
        Assert.Contains("được duyệt nghỉ", leaveError.Message);

        db.CoachLeaves.RemoveRange(db.CoachLeaves);
        var assignedUser = new User
        {
            RoleId = coachRoleId,
            Username = "coach_already_assigned",
            Email = "coach_already_assigned@example.com",
            PasswordHash = "hash",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(assignedUser);
        await db.SaveChangesAsync();
        var assignedCoach = new CoachProfile
        {
            UserId = assignedUser.Id,
            CenterId = center.Id,
            CoachCode = "CHUC1302",
            FullName = "Existing Coach",
            Specialization = "Gym",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.CoachProfiles.Add(assignedCoach);
        await db.SaveChangesAsync();
        db.ClassCoaches.Add(new ClassCoach { ClassId = classEntity.Id, CoachId = assignedCoach.Id, IsPrimary = true });
        await db.SaveChangesAsync();

        var secondCoachError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignCoachToClassAsync(classEntity.Id, new AssignCoachRequest { CoachId = coach.Id }));
        Assert.Contains("đã có HLV", secondCoachError.Message);
    }

    [Fact]
    public async Task Test_UC13_RejectsAssignmentsExceedingEightTeachingHoursPerDay()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedTestDataAsync(db);
        var center = await db.Centers.FirstAsync();
        var sport = new Sport { Name = "Gym", Status = "Active" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var targetClass = new ClassEntity
        {
            CenterId = center.Id, SportId = sport.Id, Name = "Target",
            Capacity = 10, DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow
        };
        var existingClass = new ClassEntity
        {
            CenterId = center.Id, SportId = sport.Id, Name = "Existing",
            Capacity = 10, DurationMinutes = 480, Status = "Published", CreatedAt = DateTime.UtcNow
        };
        db.Classes.AddRange(targetClass, existingClass);
        await db.SaveChangesAsync();
        db.ClassSchedules.AddRange(
            new ClassSchedule
            {
                ClassId = targetClass.Id, DayOfWeek = (int)today.DayOfWeek,
                StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0),
                StartDate = today, EndDate = today.AddDays(7), Status = "Active"
            },
            new ClassSchedule
            {
                ClassId = existingClass.Id, DayOfWeek = (int)today.DayOfWeek,
                StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(7, 30),
                StartDate = today, EndDate = today.AddDays(7), Status = "Active"
            });
        await db.SaveChangesAsync();

        var coachRoleId = (await db.Roles.SingleAsync(role => role.Name == "Coach")).Id;
        var user = new User
        {
            RoleId = coachRoleId, Username = "coach_daily_limit", Email = "coach_daily_limit@example.com",
            PasswordHash = "hash", Status = "Active", CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var coach = new CoachProfile
        {
            UserId = user.Id, CenterId = center.Id, CoachCode = "CHLIMIT01",
            FullName = "Coach Daily Limit", Specialization = "Gym", Status = "Active", CreatedAt = DateTime.UtcNow
        };
        db.CoachProfiles.Add(coach);
        await db.SaveChangesAsync();
        db.ClassCoaches.Add(new ClassCoach { ClassId = existingClass.Id, CoachId = coach.Id, IsPrimary = true });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ClassService(new UnitOfWork(db)).AssignCoachToClassAsync(
                targetClass.Id, new AssignCoachRequest { CoachId = coach.Id }));

        Assert.Contains("8.0 giờ/ngày", error.Message);
    }
}

<<<<<<< Updated upstream
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsCenterManagement.API.Controllers;
=======
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
>>>>>>> Stashed changes
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.DTOs.Roles;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;
<<<<<<< Updated upstream
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
        await DbInitializer.SeedAsync(db);

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

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var coachResponse = Assert.IsType<ClassCoachResponse>(okResult.Value);

        Assert.Equal(classEntity.Id, coachResponse.ClassId);
        Assert.Equal(coachProfile.Id, coachResponse.CoachId);
        Assert.Equal("Nguyễn Văn HLV", coachResponse.CoachName);
        Assert.True(coachResponse.IsPrimary);

        var classCoachInDb = await db.ClassCoaches.SingleOrDefaultAsync(cc => cc.ClassId == classEntity.Id && cc.CoachId == coachProfile.Id);
        Assert.NotNull(classCoachInDb);
    }

    [Fact]
    public async Task Test_AssignCoachToClass_Fails_WhenScheduleOverlaps()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await DbInitializer.SeedAsync(db);

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
        await DbInitializer.SeedAsync(db);

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
        await DbInitializer.SeedAsync(db);

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
        await DbInitializer.SeedAsync(db);

        var unitOfWork = new UnitOfWork(db);
        var roleService = new RolePermissionService(unitOfWork);

        // Thử tạo vai trò trùng tên "Admin" (viết hoa/thường)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            roleService.CreateRoleAsync(new CreateRoleRequest { Name = "admin" }, CancellationToken.None));

        Assert.Contains("đã tồn tại trong hệ thống", ex.Message);
    }

    [Fact]
    public async Task Test_UC15_DeleteRole_Fails_WhenSystemRoleOrUsersAssigned()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await DbInitializer.SeedAsync(db);

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
        await DbInitializer.SeedAsync(db);

        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
        var adminUser = await db.Users.FirstAsync(u => u.Email == "admin@sportscenter.vn");

        var unitOfWork = new UnitOfWork(db);
        var roleService = new RolePermissionService(unitOfWork);
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
            roleService.UpdateRolePermissionsAsync(adminRole.Id, updateRequestSelfLockout, adminUser.Id, CancellationToken.None));

        Assert.Contains("ROLE_MANAGE", exSelfLockout.Message);

        // 2. Kiểm tra tự động bổ sung quyền cha CLASS_VIEW khi gán quyền con COACH_ASSIGN cho Role khác
        var managerRole = await db.Roles.FirstAsync(r => r.Name == "Manager");
        var updateRequestChildOnly = new UpdateRolePermissionsRequest
        {
            PermissionIds = [classAssignPerm.Id] // Chỉ chọn COACH_ASSIGN, không chọn CLASS_VIEW
        };

        var result = await roleService.UpdateRolePermissionsAsync(managerRole.Id, updateRequestChildOnly, null, CancellationToken.None);

        Assert.Contains(result.Permissions, p => p.Code == "COACH_ASSIGN");
        Assert.Contains(result.Permissions, p => p.Code == "CLASS_VIEW"); // Tự động bổ sung quyền cha CLASS_VIEW
=======

namespace SportsCenterManagement.Tests;

public sealed class UC13AndUC15Tests
{
    private static SportsCenterDbContext NewDb() => new(new DbContextOptionsBuilder<SportsCenterDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    [Fact]
    public async Task Seed_CreatesRolesPermissionsAndIsIdempotentForInMemoryProvider()
    {
        await using var db = NewDb();
        await DbInitializer.SeedAsync(db);
        await DbInitializer.SeedAsync(db);

        Assert.Contains(await db.Roles.Select(x => x.Name).ToListAsync(), x => x == "Coach");
        Assert.Contains(await db.Permissions.Select(x => x.Code).ToListAsync(), x => x == "ROLE_MANAGE");
        var manager = await db.Roles.SingleAsync(x => x.Name == "Manager");
        var roleManagementId = await db.Permissions.Where(x => x.Code == "ROLE_MANAGE").Select(x => x.Id).SingleAsync();
        Assert.True(await db.RolePermissions.AnyAsync(x => x.RoleId == manager.Id && x.PermissionId == roleManagementId));
    }

    [Fact]
    public async Task Uc13_AssignsCoachWhenScheduleDoesNotOverlap()
    {
        await using var db = NewDb();
        var setup = await ClassSetup.Create(db);
        var sourceClass = await setup.AddClass("Pilates", "Active");
        var targetClass = await setup.AddClass("Yoga", "Published");
        await setup.AddSchedule(sourceClass.Id, 1, 8, 0, 9);
        await setup.AddSchedule(targetClass.Id, 1, 9, 0, 10);
        db.ClassCoaches.Add(new ClassCoach { ClassId = sourceClass.Id, CoachId = setup.Coach.Id });
        await db.SaveChangesAsync();

        var result = await setup.Service.AssignCoachToClassAsync(targetClass.Id,
            new AssignCoachRequest { CoachId = setup.Coach.Id });

        Assert.Equal(setup.Coach.Id, result.CoachId);
        Assert.True(await db.ClassCoaches.AnyAsync(x => x.ClassId == targetClass.Id && x.CoachId == setup.Coach.Id));
    }

    [Theory]
    [InlineData(8, 9, 8, 9)]
    [InlineData(7, 30, 8, 30)]
    public async Task Uc13_RejectsFullAndPartialScheduleOverlap(int existingStartHour, int existingStartMinute,
        int targetStartHour, int targetStartMinute)
    {
        await using var db = NewDb();
        var setup = await ClassSetup.Create(db);
        var existing = await setup.AddClass("Existing", "Published");
        var target = await setup.AddClass("Target", "Published");
        await setup.AddSchedule(existing.Id, 1, existingStartHour, existingStartMinute,
            existingStartHour == 7 ? 9 : existingStartHour + 1);
        await setup.AddSchedule(target.Id, 1, targetStartHour, targetStartMinute, 10);
        db.ClassCoaches.Add(new ClassCoach { ClassId = existing.Id, CoachId = setup.Coach.Id });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.AssignCoachToClassAsync(target.Id,
            new AssignCoachRequest { CoachId = setup.Coach.Id }));
    }

    [Theory]
    [InlineData("Suspended", "Active")]
    [InlineData("Active", "Suspended")]
    public async Task Uc13_RejectsInactiveCoachOrUser(string profileStatus, string userStatus)
    {
        await using var db = NewDb();
        var setup = await ClassSetup.Create(db, profileStatus, userStatus);
        var target = await setup.AddClass("Target", "Published");
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.AssignCoachToClassAsync(target.Id,
            new AssignCoachRequest { CoachId = setup.Coach.Id }));
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task Uc13_RejectsCancelledOrCompletedClass(string status)
    {
        await using var db = NewDb();
        var setup = await ClassSetup.Create(db);
        var target = await setup.AddClass("Target", status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.AssignCoachToClassAsync(target.Id,
            new AssignCoachRequest { CoachId = setup.Coach.Id }));
    }

    [Fact]
    public async Task Uc13_RejectsWrongSpecialization()
    {
        await using var db = NewDb();
        var setup = await ClassSetup.Create(db, specialization: "Yoga");
        var boxing = await setup.AddSport("Boxing");
        var target = await setup.AddClass("Boxing class", "Published", boxing.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.AssignCoachToClassAsync(target.Id,
            new AssignCoachRequest { CoachId = setup.Coach.Id }));
    }

    [Fact]
    public async Task Uc13_RejectsSecondCoachForSingleCoachSlot()
    {
        await using var db = NewDb();
        var setup = await ClassSetup.Create(db);
        var target = await setup.AddClass("Target", "Published");
        var second = await setup.AddAnotherCoach();
        db.ClassCoaches.Add(new ClassCoach { ClassId = target.Id, CoachId = setup.Coach.Id, IsPrimary = true });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.AssignCoachToClassAsync(target.Id,
            new AssignCoachRequest { CoachId = second.Id }));
    }

    [Fact]
    public async Task Uc13_RejectsMoreThanEightTeachingHoursPerDay()
    {
        await using var db = NewDb();
        var setup = await ClassSetup.Create(db);
        var existing = await setup.AddClass("Existing", "Published");
        var target = await setup.AddClass("Target", "Published");
        await setup.AddSchedule(existing.Id, 1, 0, 0, 7);
        await setup.AddSchedule(target.Id, 1, 7, 0, 9);
        db.ClassCoaches.Add(new ClassCoach { ClassId = existing.Id, CoachId = setup.Coach.Id });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.AssignCoachToClassAsync(target.Id,
            new AssignCoachRequest { CoachId = setup.Coach.Id }));
    }

    [Fact]
    public async Task Uc15_RejectsDuplicateRoleNameCaseInsensitively()
    {
        await using var db = NewDb();
        var service = await RoleSetup.Create(db);
        db.Roles.Add(new Role { Name = "Operations", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(
            new SaveRoleRequest { Name = "operations", PermissionCodes = ["MEMBER_VIEW"] }));
    }

    [Fact]
    public async Task Uc15_RejectsMissingNameOrPermission()
    {
        await using var db = NewDb();
        var service = await RoleSetup.Create(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(
            new SaveRoleRequest { Name = "  ", PermissionCodes = ["MEMBER_VIEW"] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(
            new SaveRoleRequest { Name = "Empty", PermissionCodes = [] }));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Super Admin")]
    public async Task Uc15_ProtectsSystemRolesFromEditAndDelete(string name)
    {
        await using var db = NewDb();
        var service = await RoleSetup.Create(db);
        var role = new Role { Name = name, CreatedAt = DateTime.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var request = new SaveRoleRequest { Name = name + " edited", PermissionCodes = ["MEMBER_VIEW"] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRoleAsync(role.Id, request, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync(role.Id));
    }

    [Fact]
    public async Task Uc15_PreventsActorFromRemovingOwnRoleManagementPermission()
    {
        await using var db = NewDb();
        var service = await RoleSetup.Create(db);
        var role = new Role { Name = "Operations", CreatedAt = DateTime.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var actor = new User { Username = "manager", Email = "manager@test.local", PasswordHash = "hash",
            RoleId = role.Id, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Users.Add(actor);
        var roleViewId = await db.Permissions.Where(x => x.Code == "ROLE_VIEW").Select(x => x.Id).SingleAsync();
        var roleManageId = await db.Permissions.Where(x => x.Code == "ROLE_MANAGE").Select(x => x.Id).SingleAsync();
        db.RolePermissions.AddRange(new RolePermission { RoleId = role.Id, PermissionId = roleViewId },
            new RolePermission { RoleId = role.Id, PermissionId = roleManageId });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRoleAsync(role.Id,
            new SaveRoleRequest { Name = role.Name, PermissionCodes = ["ROLE_VIEW", "MEMBER_VIEW"] }, actor.Id));
    }

    [Fact]
    public async Task Uc15_RequiresViewWhenGrantingWritePermission()
    {
        await using var db = NewDb();
        var service = await RoleSetup.Create(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(
            new SaveRoleRequest { Name = "Editors", PermissionCodes = ["MEMBER_DELETE"] }));
    }

    [Fact]
    public async Task Uc15_RejectsDeletingRoleAssignedToTenUsers()
    {
        await using var db = NewDb();
        var service = await RoleSetup.Create(db);
        var role = new Role { Name = "Staff", CreatedAt = DateTime.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        for (var i = 0; i < 10; i++)
            db.Users.Add(new User { Username = $"staff{i}", Email = $"staff{i}@test.local", PasswordHash = "hash",
                RoleId = role.Id, Status = "Active", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync(role.Id));
        Assert.Contains("10", error.Message);
    }

    private sealed class ClassSetup(SportsCenterDbContext db, Center center, Sport sport, User coachUser,
        CoachProfile coach)
    {
        public CoachProfile Coach => coach;
        public ClassService Service => new(new UnitOfWork(db));

        public static async Task<ClassSetup> Create(SportsCenterDbContext db, string profileStatus = "Active",
            string userStatus = "Active", string specialization = "Yoga")
        {
            var center = new Center { Name = "Test Center", Address = "Address", Status = "Active", CreatedAt = DateTime.UtcNow };
            db.Centers.Add(center);
            var sport = new Sport { Name = "Yoga", Status = "Active" };
            db.Sports.Add(sport);
            var role = new Role { Name = "Coach", CreatedAt = DateTime.UtcNow };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var user = new User { Username = "coach", Email = "coach@test.local", PasswordHash = "hash",
                RoleId = role.Id, Status = userStatus, CreatedAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var coach = new CoachProfile { UserId = user.Id, CenterId = center.Id, CoachCode = "C001",
                FullName = "Coach One", Specialization = specialization, Status = profileStatus, CreatedAt = DateTime.UtcNow };
            db.CoachProfiles.Add(coach);
            await db.SaveChangesAsync();
            return new ClassSetup(db, center, sport, user, coach);
        }

        public async Task<Sport> AddSport(string name)
        {
            var item = new Sport { Name = name, Status = "Active" };
            db.Sports.Add(item);
            await db.SaveChangesAsync();
            return item;
        }

        public async Task<ClassEntity> AddClass(string name, string status, long? sportId = null)
        {
            var item = new ClassEntity { CenterId = center.Id, SportId = sportId ?? sport.Id, Name = name,
                Capacity = 10, DurationMinutes = 60, Status = status, CreatedAt = DateTime.UtcNow };
            db.Classes.Add(item);
            await db.SaveChangesAsync();
            return item;
        }

        public async Task AddSchedule(long classId, int day, int startHour, int startMinute, int endHour)
        {
            db.ClassSchedules.Add(new ClassSchedule { ClassId = classId, DayOfWeek = day,
                StartTime = new TimeOnly(startHour, startMinute), EndTime = new TimeOnly(endHour, 0), Status = "Active" });
            await db.SaveChangesAsync();
        }

        public async Task<CoachProfile> AddAnotherCoach()
        {
            var user = new User { Username = "coach2", Email = "coach2@test.local", PasswordHash = "hash",
                RoleId = coachUser.RoleId, Status = "Active", CreatedAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var item = new CoachProfile { UserId = user.Id, CenterId = center.Id, CoachCode = "C002",
                FullName = "Coach Two", Specialization = "Yoga", Status = "Active", CreatedAt = DateTime.UtcNow };
            db.CoachProfiles.Add(item);
            await db.SaveChangesAsync();
            return item;
        }
    }

    private sealed class RoleSetup
    {
        public static async Task<RolePermissionService> Create(SportsCenterDbContext db)
        {
            var permissions = new[]
            {
                new Permission { Code = "MEMBER_VIEW", Name = "View members" },
                new Permission { Code = "MEMBER_DELETE", Name = "Delete members" },
                new Permission { Code = "ROLE_VIEW", Name = "View roles" },
                new Permission { Code = "ROLE_MANAGE", Name = "Manage roles" }
            };
            db.Permissions.AddRange(permissions);
            await db.SaveChangesAsync();
            return new RolePermissionService(db);
        }
>>>>>>> Stashed changes
    }
}

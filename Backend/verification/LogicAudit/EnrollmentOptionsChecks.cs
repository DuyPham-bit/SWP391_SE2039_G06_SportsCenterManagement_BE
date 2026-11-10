using System.Net;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

/// <summary>
/// Member self-service sport/coach enrollment: the options tree must only offer classes the member can
/// really join, and must explain blocked choices with the same rules the enrollment write path enforces.
/// </summary>
static class EnrollmentOptionsChecks
{
    public static async Task Run(SportsCenterDbContext db, Func<string, Func<Task<bool>>, Task> check)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        var center = new Center { Name = "Enroll center", Address = "Audit", Status = "Active", CreatedAt = now };
        var otherCenter = new Center { Name = "Enroll other center", Address = "Audit", Status = "Active", CreatedAt = now };
        var memberRole = new Role { Name = "EnrollAuditMember" };
        var coachRole = new Role { Name = "EnrollAuditCoach" };
        db.AddRange(center, otherCenter, memberRole, coachRole);
        await db.SaveChangesAsync();

        User NewUser(string name, long roleId) => new()
        {
            Username = name, Email = name + "@enroll.test", PasswordHash = "test-only", RoleId = roleId,
            Status = "Active", CreatedAt = now
        };
        var memberUser = NewUser("enroll-member", memberRole.Id);
        var noPackageUser = NewUser("enroll-nopkg", memberRole.Id);
        var foreignUser = NewUser("enroll-foreign", memberRole.Id);
        var coachAUser = NewUser("enroll-coach-a", coachRole.Id);
        var coachBUser = NewUser("enroll-coach-b", coachRole.Id);
        var coachCUser = NewUser("enroll-coach-c", coachRole.Id);
        db.AddRange(memberUser, noPackageUser, foreignUser, coachAUser, coachBUser, coachCUser);
        await db.SaveChangesAsync();

        var swim = new Sport { CenterId = center.Id, Name = "Enroll Swim", Status = "Active" };
        var gym = new Sport { CenterId = center.Id, Name = "Enroll Gym", Status = "Active" };
        var room = new Room { CenterId = center.Id, Name = "Enroll Pool", Capacity = 30, Status = "Active", CreatedAt = now };
        db.AddRange(swim, gym, room);
        await db.SaveChangesAsync();

        CoachProfile NewCoach(User user, string code, string name, int years) => new()
        {
            UserId = user.Id, CenterId = center.Id, CoachCode = code, FullName = name, Specialization = "Audit",
            ExperienceYears = years, Status = "Active", CreatedAt = now
        };
        var coachA = NewCoach(coachAUser, "ENR-A", "Coach Anh", 8);
        var coachB = NewCoach(coachBUser, "ENR-B", "Coach Binh", 3);
        var coachC = NewCoach(coachCUser, "ENR-C", "Coach Chi", 5);
        db.AddRange(coachA, coachB, coachC);
        await db.SaveChangesAsync();

        ClassEntity NewClass(Sport sport, string name, int capacity, string status = "Published") => new()
        {
            CenterId = center.Id, SportId = sport.Id, RoomId = room.Id, Name = name, Capacity = capacity,
            DurationMinutes = 60, Status = status, CreatedAt = now
        };
        var swimA = NewClass(swim, "Enroll swim with A", 10);
        var swimB = NewClass(swim, "Enroll swim with B", 10);
        var gymC = NewClass(gym, "Enroll gym with C", 10);
        var gymFull = NewClass(gym, "Enroll gym full", 1);
        var draft = NewClass(gym, "Enroll draft class", 10, "Draft");
        var noCoach = NewClass(gym, "Enroll class without coach", 10);
        db.AddRange(swimA, swimB, gymC, gymFull, draft, noCoach);
        await db.SaveChangesAsync();

        db.AddRange(
            new ClassCoach { ClassId = swimA.Id, CoachId = coachA.Id, IsPrimary = true },
            new ClassCoach { ClassId = swimB.Id, CoachId = coachB.Id, IsPrimary = true },
            new ClassCoach { ClassId = gymC.Id, CoachId = coachC.Id, IsPrimary = true },
            new ClassCoach { ClassId = gymFull.Id, CoachId = coachC.Id, IsPrimary = true },
            new ClassCoach { ClassId = draft.Id, CoachId = coachC.Id, IsPrimary = true },
            new ClassSchedule { ClassId = swimA.Id, RoomId = room.Id, DayOfWeek = 1, StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(8, 0), StartDate = today.AddDays(-10), EndDate = today.AddDays(60), Status = "Active" },
            new ClassSchedule { ClassId = swimA.Id, RoomId = room.Id, DayOfWeek = 3, StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(8, 0), StartDate = today.AddDays(-10), EndDate = today.AddDays(60), Status = "Active" },
            new ClassSchedule { ClassId = swimA.Id, RoomId = room.Id, DayOfWeek = 5, StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0), StartDate = today.AddDays(-10), EndDate = today.AddDays(60), Status = "Cancelled" });
        await db.SaveChangesAsync();

        var package = new MembershipPackage
        {
            CenterId = center.Id, Name = "Enroll one-sport package", DurationDays = 30, Price = 500000, MaxClasses = 3,
            AllowedSports = 1, Status = "Active", CreatedAt = now
        };
        db.Add(package);
        await db.SaveChangesAsync();

        var member = new MemberProfile { UserId = memberUser.Id, CenterId = center.Id, MemberCode = "ENR-MEM", FullName = "Enroll member", CreatedAt = now };
        var noPackage = new MemberProfile { UserId = noPackageUser.Id, CenterId = center.Id, MemberCode = "ENR-NOPKG", FullName = "No package", CreatedAt = now };
        var foreign = new MemberProfile { UserId = foreignUser.Id, CenterId = otherCenter.Id, MemberCode = "ENR-FOREIGN", FullName = "Foreign", CreatedAt = now };
        db.AddRange(member, noPackage, foreign);
        await db.SaveChangesAsync();

        var subscription = new MemberSubscription
        {
            MemberId = member.Id, PackageId = package.Id, StartDate = today.AddDays(-5), EndDate = today.AddDays(25),
            DurationDays = 30, Price = 500000, Status = "Active", CreatedAt = now
        };
        db.Add(subscription);
        await db.SaveChangesAsync();

        // The member already joined the swim class of coach A; another member fills the one-seat gym class.
        db.AddRange(
            new ClassEnrollment { ClassId = swimA.Id, MemberId = member.Id, SubscriptionId = subscription.Id, RegisteredAt = now, Status = "Confirmed" },
            new ClassEnrollment { ClassId = gymFull.Id, MemberId = noPackage.Id, RegisteredAt = now, Status = "Confirmed" },
            new CoachReview { CoachId = coachB.Id, MemberId = member.Id, Rating = 5, CreatedAt = now },
            new CoachReview { CoachId = coachB.Id, MemberId = noPackage.Id, Rating = 4, CreatedAt = now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var clock = new BusinessClock(new ConfigurationBuilder().Build());
        MemberEnrollmentOptionsService Service() => new(db, clock);

        EnrollmentClassOptionDto? FindClass(MemberEnrollmentOptionsDto options, long classId) =>
            options.Sports.SelectMany(s => s.Coaches).SelectMany(c => c.Classes).SingleOrDefault(c => c.ClassId == classId);

        await check("enrollment options group published classes by sport and primary coach", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            var swimOption = options.Sports.Single(s => s.SportId == swim.Id);
            var gymOption = options.Sports.Single(s => s.SportId == gym.Id);
            return swimOption.Coaches.Select(c => c.CoachId).OrderBy(id => id).SequenceEqual(new[] { coachA.Id, coachB.Id }.OrderBy(id => id))
                && gymOption.Coaches.Single().CoachId == coachC.Id
                && gymOption.Coaches.Single().Classes.Select(c => c.ClassId).OrderBy(id => id)
                    .SequenceEqual(new[] { gymC.Id, gymFull.Id }.OrderBy(id => id));
        });

        await check("enrollment options hide draft classes and classes without an active primary coach", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            return FindClass(options, draft.Id) is null && FindClass(options, noCoach.Id) is null;
        });

        await check("enrollment options expose active weekly schedules, seats and coach rating", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            var swimClass = FindClass(options, swimA.Id)!;
            var coachBOption = options.Sports.SelectMany(s => s.Coaches).Single(c => c.CoachId == coachB.Id);
            return swimClass.Schedules.Count == 2 && swimClass.Schedules.All(s => s.DayOfWeek is 1 or 3)
                && swimClass.EnrolledCount == 1 && swimClass.Capacity == 10 && swimClass.RoomName == "Enroll Pool"
                && coachBOption.ReviewCount == 2 && coachBOption.AverageRating == 4.5
                && coachBOption.ExperienceYears == 3;
        });

        await check("an enrolled class reports its enrollment instead of offering a second sign-up", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            var swimClass = FindClass(options, swimA.Id)!;
            return swimClass.EnrollmentId.HasValue && swimClass.EligibleSubscriptionId is null
                && options.Sports.Single(s => s.SportId == swim.Id).IsEnrolled;
        });

        await check("same sport with a different coach stays eligible within a one-sport package", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            var other = FindClass(options, swimB.Id)!;
            return other.EligibleSubscriptionId == subscription.Id && other.BlockedReason is null;
        });

        await check("a new sport beyond the package sport allowance is blocked with a reason", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            var gymClass = FindClass(options, gymC.Id)!;
            var summary = options.Subscriptions.Single();
            return gymClass.EligibleSubscriptionId is null && gymClass.BlockedCode == "SPORT_LIMIT"
                && summary.SubscriptionId == subscription.Id && summary.AllowedSports == 1
                && summary.UsedSportIds.SequenceEqual(new[] { swim.Id });
        });

        await check("a full class is blocked with a capacity reason before package limits", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            var full = FindClass(options, gymFull.Id)!;
            return full.BlockedCode == "CLASS_FULL" && full.EnrolledCount == 1 && full.EligibleSubscriptionId is null;
        });

        await check("a member without an active package can browse but every class asks for a package", async () =>
        {
            var options = await Service().GetOptionsAsync(noPackageUser.Id, center.Id);
            var classes = options.Sports.SelectMany(s => s.Coaches).SelectMany(c => c.Classes)
                .Where(c => c.EnrollmentId is null && c.ClassId != gymFull.Id).ToArray();
            return options.Subscriptions.Count == 0 && classes.Length > 0
                && classes.All(c => c.EligibleSubscriptionId is null && c.BlockedCode == "NO_PACKAGE");
        });

        await check("a member cannot read enrollment options of another center", async () =>
        {
            try
            {
                await Service().GetOptionsAsync(foreignUser.Id, center.Id);
                return false;
            }
            catch (BusinessException error) { return error.StatusCode == HttpStatusCode.Forbidden; }
        });

        await check("offered eligible class enrolls successfully through the real write path", async () =>
        {
            var options = await Service().GetOptionsAsync(memberUser.Id, center.Id);
            var target = FindClass(options, swimB.Id)!;
            var core = new CoreFlowService(new UnitOfWork(db), new ConfigurationBuilder().Build());
            var enrollment = await core.EnrollMemberAsync(swimB.Id, member.Id, target.EligibleSubscriptionId!.Value, memberUser.Id);
            db.ChangeTracker.Clear();
            var after = FindClass(await Service().GetOptionsAsync(memberUser.Id, center.Id), swimB.Id)!;
            return enrollment.Status == "Confirmed" && after.EnrollmentId == enrollment.Id;
        });

        await check("write path rejects the sport the options marked as over the allowance", async () =>
        {
            var core = new CoreFlowService(new UnitOfWork(db), new ConfigurationBuilder().Build());
            try
            {
                await core.EnrollMemberAsync(gymC.Id, member.Id, subscription.Id, memberUser.Id);
                return false;
            }
            catch (InvalidOperationException error) { return error.Message.Contains("sport allowance", StringComparison.OrdinalIgnoreCase); }
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;
using Xunit;

namespace SportsCenterManagement.Tests;

public class Flow2ClassBookingTests
{
    private sealed class Fixture
    {
        public required SportsCenterDbContext Db { get; init; }
        public required Center Center { get; init; }
        public required Sport Sport { get; init; }
        public required Room Room { get; init; }
        public required ClassEntity Class { get; init; }
        public ClassManagementService Classes => new(new UnitOfWork(Db));
        public ClassService ClassDirectory => new(new UnitOfWork(Db));
        public ClassScheduleQueryService Queries => new(new UnitOfWork(Db));
        public SessionBookingService Bookings => new(new UnitOfWork(Db));
    }

    private static DateTime Now => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;

    private static async Task<Fixture> CreateFixtureAsync(int classCapacity = 8, int roomCapacity = 10)
    {
        var options = new DbContextOptionsBuilder<SportsCenterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new SportsCenterDbContext(options);
        await DbInitializer.SeedAsync(db);

        var center = await db.Centers.FirstAsync();
        var sport = new Sport { Name = "Yoga", Status = "Active" };
        db.Sports.Add(sport);
        var room = new Room { CenterId = center.Id, Name = "Room A", Capacity = roomCapacity, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var cls = new ClassEntity
        {
            CenterId = center.Id, SportId = sport.Id, RoomId = room.Id, Name = "Yoga Sang",
            Capacity = classCapacity, DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow
        };
        db.Classes.Add(cls);
        await db.SaveChangesAsync();
        return new Fixture { Db = db, Center = center, Sport = sport, Room = room, Class = cls };
    }

    private static async Task<ClassSession> AddSessionAsync(Fixture f, TimeSpan fromNow, ClassEntity? cls = null, int minutes = 60)
    {
        var start = Now + fromNow;
        var session = new ClassSession
        {
            ClassId = (cls ?? f.Class).Id,
            RoomId = f.Room.Id,
            SessionDate = DateOnly.FromDateTime(start),
            StartTime = TimeOnly.FromDateTime(start),
            EndTime = TimeOnly.FromDateTime(start.AddMinutes(minutes)),
            SessionStatus = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };
        f.Db.ClassSessions.Add(session);
        await f.Db.SaveChangesAsync();
        return session;
    }

    private static async Task<(User User, MemberProfile Member)> AddMemberAsync(Fixture f, string name, bool withPackage = true)
    {
        var role = await f.Db.Roles.FirstAsync(r => r.Name == "Member");
        var user = new User
        {
            RoleId = role.Id, Username = name, Email = $"{name}@scms.vn",
            PasswordHash = "x", Status = "Active", CreatedAt = DateTime.UtcNow
        };
        f.Db.Users.Add(user);
        await f.Db.SaveChangesAsync();
        var member = new MemberProfile { UserId = user.Id, MemberCode = $"M-{name}", FullName = name, CreatedAt = DateTime.UtcNow };
        f.Db.MemberProfiles.Add(member);
        await f.Db.SaveChangesAsync();
        if (withPackage)
        {
            f.Db.MemberSubscriptions.Add(new MemberSubscription
            {
                MemberId = member.Id, PackageId = 1, Status = "Active", Price = 1,
                StartDate = DateOnly.FromDateTime(Now.AddDays(-5)), EndDate = DateOnly.FromDateTime(Now.AddDays(60)),
                CreatedAt = DateTime.UtcNow
            });
            await f.Db.SaveChangesAsync();
        }
        return (user, member);
    }

    private static async Task<FlowException> AssertFlowAsync(int status, Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<FlowException>(action);
        Assert.Equal(status, ex.StatusCode);
        return ex;
    }

    // ---------------------------------------------------------------- UC-12

    [Fact]
    public async Task CreateClass_Fails_WhenCapacityExceedsRoom()
    {
        var f = await CreateFixtureAsync();
        await AssertFlowAsync(400, () => f.Classes.CreateClassAsync(new CreateClassRequest(
            f.Center.Id, f.Sport.Id, f.Room.Id, "Too big", null, null, 11, 60)));
    }

    [Fact]
    public async Task CreateClass_Fails_ForInvalidInputAndMissingReferences()
    {
        var f = await CreateFixtureAsync();
        await AssertFlowAsync(400, () => f.Classes.CreateClassAsync(new CreateClassRequest(f.Center.Id, f.Sport.Id, null, " ", null, null, 5, 60)));
        await AssertFlowAsync(400, () => f.Classes.CreateClassAsync(new CreateClassRequest(f.Center.Id, f.Sport.Id, null, "X", null, null, 0, 60)));
        await AssertFlowAsync(400, () => f.Classes.CreateClassAsync(new CreateClassRequest(f.Center.Id, f.Sport.Id, null, "X", null, null, 5, 0)));
        await AssertFlowAsync(404, () => f.Classes.CreateClassAsync(new CreateClassRequest(f.Center.Id, 9999, null, "X", null, null, 5, 60)));
        await AssertFlowAsync(404, () => f.Classes.CreateClassAsync(new CreateClassRequest(f.Center.Id, f.Sport.Id, 9999, "X", null, null, 5, 60)));
    }

    [Fact]
    public async Task CreateSchedule_GeneratesSessions_AndRejectsRoomClash()
    {
        var f = await CreateFixtureAsync();
        var today = DateOnly.FromDateTime(Now);
        var request = new CreateClassScheduleRequest(null, (int)today.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0), today, today.AddDays(13));

        var created = await f.Classes.CreateScheduleAsync(f.Class.Id, request);
        Assert.True(created.GeneratedSessions >= 2);
        Assert.Equal(created.GeneratedSessions, await f.Db.ClassSessions.CountAsync(s => s.ScheduleId == created.Id));

        var other = new ClassEntity
        {
            CenterId = f.Center.Id, SportId = f.Sport.Id, RoomId = f.Room.Id, Name = "Pilates",
            Capacity = 5, DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow
        };
        f.Db.Classes.Add(other);
        await f.Db.SaveChangesAsync();

        var ex = await AssertFlowAsync(409, () => f.Classes.CreateScheduleAsync(other.Id,
            request with { StartTime = new TimeOnly(9, 30), EndTime = new TimeOnly(10, 30) }));
        Assert.Contains("Yoga Sang", ex.Message);

        // Adjacent slot is fine.
        await f.Classes.CreateScheduleAsync(other.Id, request with { StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) });
    }

    [Fact]
    public async Task CancelClass_SoftCancels_AndReportsAffectedMembers()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (user, _) = await AddMemberAsync(f, "alice");
        await f.Bookings.BookSessionAsync(user.Id, session.Id);

        var result = await f.Classes.CancelClassAsync(f.Class.Id);

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal(1, result.AffectedBookings);
        Assert.Equal("Cancelled", (await f.Db.Classes.SingleAsync(c => c.Id == f.Class.Id)).Status);
        await AssertFlowAsync(409, () => f.Classes.CancelClassAsync(f.Class.Id));
    }

    // ---------------------------------------------------------------- UC-42 / UC-30

    [Fact]
    public async Task GetSessions_ValidatesPagingAndReturnsEmptyList()
    {
        var f = await CreateFixtureAsync();
        await AssertFlowAsync(400, () => f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null, 0, 20, f.Center.Id)));
        await AssertFlowAsync(400, () => f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null, 1, 0, f.Center.Id)));

        var empty = await f.Queries.GetSessionsAsync(new SessionScheduleQuery(f.Sport.Id + 100, null, null, null, CenterId: f.Center.Id));
        Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task GetSessions_HidesCancelledClass()
    {
        var f = await CreateFixtureAsync();
        await AddSessionAsync(f, TimeSpan.FromDays(1));
        f.Class.Status = "Cancelled";
        await f.Db.SaveChangesAsync();

        var page = await f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null, CenterId: f.Center.Id));
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task TeachingSchedule_UsesCurrentUser_AndIsEmptyWithoutAssignments()
    {
        var f = await CreateFixtureAsync();
        var (memberUser, _) = await AddMemberAsync(f, "notcoach");
        await AssertFlowAsync(403, () => f.Queries.GetTeachingScheduleAsync(memberUser.Id, null, null));

        var coachRole = await f.Db.Roles.FirstAsync(r => r.Name == "Coach");
        var coachUser = new User { RoleId = coachRole.Id, Username = "c1", Email = "c1@scms.vn", PasswordHash = "x", Status = "Active", CreatedAt = DateTime.UtcNow };
        f.Db.Users.Add(coachUser);
        await f.Db.SaveChangesAsync();
        var coach = new CoachProfile { UserId = coachUser.Id, CenterId = f.Center.Id, CoachCode = "C1", FullName = "Coach One", Status = "Active", CreatedAt = DateTime.UtcNow };
        f.Db.CoachProfiles.Add(coach);
        await f.Db.SaveChangesAsync();
        await AddSessionAsync(f, TimeSpan.FromDays(1));

        Assert.Empty(await f.Queries.GetTeachingScheduleAsync(coachUser.Id, null, null));

        f.Db.ClassCoaches.Add(new ClassCoach { ClassId = f.Class.Id, CoachId = coach.Id, IsPrimary = true });
        await f.Db.SaveChangesAsync();
        Assert.Single(await f.Queries.GetTeachingScheduleAsync(coachUser.Id, null, null));
        var session = await f.Db.ClassSessions.SingleAsync();
        var (booker, member) = await AddMemberAsync(f, "rosterbooker");
        await f.Bookings.BookSessionAsync(booker.Id, session.Id);
        var roster = await f.Queries.GetSessionRosterAsync(coachUser.Id, session.Id);
        Assert.Equal(member.Id, Assert.Single(roster).MemberId);
        await AssertFlowAsync(403, () => f.Queries.GetSessionRosterAsync(member.UserId, session.Id));
    }

    [Fact]
    public async Task SessionCatalog_IsRequiredToNameCenter_AndNeverReturnsOtherCenters()
    {
        var f = await CreateFixtureAsync();
        await AddSessionAsync(f, TimeSpan.FromDays(1));
        var otherCenter = new Center { Name = "Other center", Address = "Other", Phone = "1", Email = "other@test", Status = "Active", CreatedAt = DateTime.UtcNow };
        f.Db.Centers.Add(otherCenter);
        await f.Db.SaveChangesAsync();
        var otherClass = new ClassEntity
        {
            CenterId = otherCenter.Id, SportId = f.Sport.Id, Name = "Private class", Capacity = 10,
            DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow
        };
        f.Db.Classes.Add(otherClass);
        await f.Db.SaveChangesAsync();
        await AddSessionAsync(f, TimeSpan.FromDays(2), otherClass);

        await AssertFlowAsync(400, () => f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null)));
        var page = await f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null, CenterId: f.Center.Id));
        Assert.Single(page.Items);
        Assert.Equal(f.Class.Id, page.Items[0].ClassId);
    }

    [Fact]
    public async Task CreateClass_StaysDraft_UntilScheduleAndActiveCoachArePublished()
    {
        var f = await CreateFixtureAsync();
        var created = await f.Classes.CreateClassAsync(new CreateClassRequest(
            f.Center.Id, f.Sport.Id, f.Room.Id, "New yoga", null, null, 8, 60));
        Assert.Equal("Draft", created.Status);

        var today = DateOnly.FromDateTime(Now);
        var firstDate = today.AddDays(1);
        while ((int)firstDate.DayOfWeek != (int)today.DayOfWeek) firstDate = firstDate.AddDays(1);
        var schedule = await f.Classes.CreateScheduleAsync(created.Id,
            new CreateClassScheduleRequest(null, (int)firstDate.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0), firstDate, firstDate.AddDays(7)));
        Assert.True(schedule.GeneratedSessions > 0);
        await AssertFlowAsync(409, () => f.Classes.PublishClassAsync(created.Id));

        var role = await f.Db.Roles.SingleAsync(item => item.Name == "Coach");
        var user = new User { RoleId = role.Id, Username = "publishcoach", Email = "publishcoach@scms.vn", PasswordHash = "x", Status = "Active", CreatedAt = DateTime.UtcNow };
        f.Db.Users.Add(user);
        await f.Db.SaveChangesAsync();
        var coach = new CoachProfile { UserId = user.Id, CenterId = f.Center.Id, CoachCode = "PUB-1", FullName = "Publish Coach", Specialization = "Yoga", Status = "Active", CreatedAt = DateTime.UtcNow };
        f.Db.CoachProfiles.Add(coach);
        await f.Db.SaveChangesAsync();
        await f.ClassDirectory.AssignCoachToClassAsync(created.Id, new AssignCoachRequest { CoachId = coach.Id });

        var published = await f.Classes.PublishClassAsync(created.Id);
        Assert.Equal("Published", published.Status);
        Assert.All(await f.Db.ClassSessions.Where(item => item.ClassId == created.Id).ToListAsync(),
            item => Assert.Equal(coach.Id, item.CoachId));
    }

    [Fact]
    public async Task ScheduleCancellation_CancelsExactSessionWaitlistAndNotifiesAffectedMembers()
    {
        var f = await CreateFixtureAsync(classCapacity: 1);
        var startDate = DateOnly.FromDateTime(Now).AddDays(1);
        var schedule = new ClassSchedule
        {
            ClassId = f.Class.Id, RoomId = f.Room.Id, DayOfWeek = (int)startDate.DayOfWeek,
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0),
            StartDate = startDate, EndDate = startDate.AddDays(7), Status = "Active"
        };
        f.Db.ClassSchedules.Add(schedule);
        await f.Db.SaveChangesAsync();
        var session = new ClassSession
        {
            ClassId = f.Class.Id, ScheduleId = schedule.Id, RoomId = f.Room.Id,
            SessionDate = startDate, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0),
            SessionStatus = "Scheduled", CreatedAt = DateTime.UtcNow
        };
        f.Db.ClassSessions.Add(session);
        await f.Db.SaveChangesAsync();
        var (holder, _) = await AddMemberAsync(f, "scheduleholder");
        var (waiter, waiterMember) = await AddMemberAsync(f, "schedulewaiter");
        var holderBooking = await f.Bookings.BookSessionAsync(holder.Id, session.Id);
        await f.Bookings.BookSessionAsync(waiter.Id, session.Id);

        await f.Classes.CancelScheduleAsync(f.Class.Id, schedule.Id);

        Assert.Equal("Cancelled", (await f.Db.SessionBookings.SingleAsync(item => item.Id == holderBooking.Booking!.Id)).Status);
        Assert.Equal("Cancelled", (await f.Db.ClassWaitlists.SingleAsync()).Status);
        Assert.Contains(await f.Db.UserNotifications.Select(item => item.UserId).ToListAsync(),
            userId => userId == (f.Db.MemberProfiles.Where(item => item.Id == waiterMember.Id).Select(item => item.UserId).Single()));
    }

    [Fact]
    public async Task ReceptionistCanBookMemberOnlyAtAssignedCenter()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (_, member) = await AddMemberAsync(f, "countermember");
        var role = await f.Db.Roles.SingleAsync(item => item.Name == "Receptionist");
        var user = new User { RoleId = role.Id, Username = "desk1", Email = "desk1@scms.vn", PasswordHash = "x", Status = "Active", CreatedAt = DateTime.UtcNow };
        f.Db.Users.Add(user);
        await f.Db.SaveChangesAsync();
        f.Db.StaffProfiles.Add(new StaffProfile
        {
            UserId = user.Id, CenterId = f.Center.Id, StaffCode = "REC-1", FullName = "Receptionist",
            Position = "Receptionist", Status = "Active", CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();

        var result = await f.Bookings.BookSessionForMemberAsync(user.Id, member.Id, session.Id);
        Assert.True(result.Booked);
        Assert.Equal(user.Id, (await f.Db.SessionBookings.SingleAsync()).BookedBy);

        var profile = await f.Db.StaffProfiles.SingleAsync(item => item.UserId == user.Id);
        profile.CenterId = f.Center.Id + 100;
        await f.Db.SaveChangesAsync();
        await AssertFlowAsync(403, () => f.Bookings.BookSessionForMemberAsync(user.Id, member.Id, session.Id));
    }

    [Fact]
    public async Task Book_RejectsClassModeDuplicatesAndUnavailablePackageScopeOrQuota()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (user, member) = await AddMemberAsync(f, "modecheck");

        f.Db.ClassEnrollments.Add(new ClassEnrollment
        {
            ClassId = f.Class.Id, MemberId = member.Id, SubscriptionId = 1,
            RegisteredAt = DateTime.UtcNow, Status = "Confirmed"
        });
        await f.Db.SaveChangesAsync();
        await AssertFlowAsync(409, () => f.Bookings.BookSessionAsync(user.Id, session.Id));
        f.Db.ClassEnrollments.RemoveRange(f.Db.ClassEnrollments);

        var subscription = await f.Db.MemberSubscriptions.SingleAsync(item => item.MemberId == member.Id);
        var package = await f.Db.MembershipPackages.SingleAsync(item => item.Id == subscription.PackageId);
        package.MaxClasses = 1;
        package.AllowedSports = 1;
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Bookings.BookSessionAsync(user.Id, session.Id)).Booked);

        var secondSameSportClass = new ClassEntity
        {
            CenterId = f.Center.Id, SportId = f.Class.SportId, Name = "Second class, same sport", Capacity = 8,
            DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow
        };
        f.Db.Classes.Add(secondSameSportClass);
        await f.Db.SaveChangesAsync();
        var secondSameSportSession = await AddSessionAsync(f, TimeSpan.FromDays(2), secondSameSportClass);
        await AssertFlowAsync(403, () => f.Bookings.BookSessionAsync(user.Id, secondSameSportSession.Id));

        package.MaxClasses = null;
        await f.Db.SaveChangesAsync();
        var otherSport = new Sport { Name = "Badminton", Status = "Active" };
        f.Db.Sports.Add(otherSport);
        await f.Db.SaveChangesAsync();
        var otherSportClass = new ClassEntity
        {
            CenterId = f.Center.Id, SportId = otherSport.Id, Name = "Badminton", Capacity = 8,
            DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow
        };
        f.Db.Classes.Add(otherSportClass);
        await f.Db.SaveChangesAsync();
        var otherSportSession = await AddSessionAsync(f, TimeSpan.FromDays(3), otherSportClass);
        await AssertFlowAsync(403, () => f.Bookings.BookSessionAsync(user.Id, otherSportSession.Id));

        var otherCenter = new Center { Name = "Elsewhere", Address = "Elsewhere", Phone = "2", Email = "elsewhere@test", Status = "Active", CreatedAt = DateTime.UtcNow };
        f.Db.Centers.Add(otherCenter);
        await f.Db.SaveChangesAsync();
        var foreignPackage = new MembershipPackage
        {
            CenterId = otherCenter.Id, Name = "Foreign", DurationDays = 30, Price = 10,
            AllowedSports = 10, Status = "Active", CreatedAt = DateTime.UtcNow
        };
        f.Db.MembershipPackages.Add(foreignPackage);
        await f.Db.SaveChangesAsync();
        subscription.PackageId = foreignPackage.Id;
        subscription.EndDate = DateOnly.FromDateTime(Now.AddDays(10));
        await f.Db.SaveChangesAsync();
        var (foreignUser, _) = await AddMemberAsync(f, "foreignpack", withPackage: false);
        f.Db.MemberSubscriptions.Add(new MemberSubscription
        {
            MemberId = (await f.Db.MemberProfiles.SingleAsync(item => item.UserId == foreignUser.Id)).Id,
            PackageId = foreignPackage.Id, Status = "Active", Price = 1,
            StartDate = DateOnly.FromDateTime(Now.AddDays(-1)), EndDate = DateOnly.FromDateTime(Now.AddDays(10)), CreatedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
        await AssertFlowAsync(403, () => f.Bookings.BookSessionAsync(foreignUser.Id, session.Id));
    }

    [Fact]
    public async Task Waitlist_IsUniquePerSession_AndCancellationNeverPromotesAnotherSessionsWaiter()
    {
        var f = await CreateFixtureAsync(classCapacity: 1);
        var firstSession = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var secondSession = await AddSessionAsync(f, TimeSpan.FromDays(2));
        var (holder, _) = await AddMemberAsync(f, "waitlistholder");
        var (waiter, _) = await AddMemberAsync(f, "waitlistmember");
        var bookedFirst = await f.Bookings.BookSessionAsync(holder.Id, firstSession.Id);
        var waitlistedSecond = await f.Bookings.BookSessionAsync(holder.Id, secondSession.Id);
        var waitlist1 = await f.Bookings.BookSessionAsync(waiter.Id, secondSession.Id);
        var waitlist2 = await f.Bookings.BookSessionAsync(waiter.Id, secondSession.Id);

        Assert.True(waitlistedSecond.Booked);
        Assert.True(waitlist1.Waitlisted);
        Assert.Equal(waitlist1.WaitlistId, waitlist2.WaitlistId);
        Assert.Equal(secondSession.Id, (await f.Db.ClassWaitlists.SingleAsync()).SessionId);

        var cancellation = await f.Bookings.CancelBookingAsync(holder.Id, bookedFirst.Booking!.Id);
        Assert.Null(cancellation.PromotedMemberId);
        Assert.Equal("Waiting", (await f.Db.ClassWaitlists.SingleAsync()).Status);
    }

    [Fact]
    public async Task Book_RejectsDraftClass()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (user, _) = await AddMemberAsync(f, "draftbooker");
        f.Class.Status = "Draft";
        await f.Db.SaveChangesAsync();

        await AssertFlowAsync(409, () => f.Bookings.BookSessionAsync(user.Id, session.Id));
    }

    // ---------------------------------------------------------------- UC-43

    [Fact]
    public async Task Book_Succeeds_AndRejectsDuplicate()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (user, _) = await AddMemberAsync(f, "bob");

        var result = await f.Bookings.BookSessionAsync(user.Id, session.Id);
        Assert.True(result.Booked);
        await AssertFlowAsync(409, () => f.Bookings.BookSessionAsync(user.Id, session.Id));
        Assert.Equal(1, await f.Db.SessionBookings.CountAsync());
    }

    [Fact]
    public async Task Book_Fails_WithoutActivePackage()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (user, _) = await AddMemberAsync(f, "nopack", withPackage: false);
        await AssertFlowAsync(403, () => f.Bookings.BookSessionAsync(user.Id, session.Id));
    }

    [Fact]
    public async Task Book_Fails_OutsideBookingWindow()
    {
        var f = await CreateFixtureAsync();
        var (user, _) = await AddMemberAsync(f, "window");
        var tooSoon = await AddSessionAsync(f, TimeSpan.FromMinutes(10));
        var tooFar = await AddSessionAsync(f, TimeSpan.FromDays(10));
        await AssertFlowAsync(400, () => f.Bookings.BookSessionAsync(user.Id, tooSoon.Id));
        await AssertFlowAsync(400, () => f.Bookings.BookSessionAsync(user.Id, tooFar.Id));
    }

    [Fact]
    public async Task Book_Fails_ForMissingOrCancelledSession_AndOverlap()
    {
        var f = await CreateFixtureAsync();
        var (user, _) = await AddMemberAsync(f, "overlap");
        await AssertFlowAsync(404, () => f.Bookings.BookSessionAsync(user.Id, 9999));

        var cancelled = await AddSessionAsync(f, TimeSpan.FromDays(1));
        cancelled.SessionStatus = "Cancelled";
        await f.Db.SaveChangesAsync();
        await AssertFlowAsync(409, () => f.Bookings.BookSessionAsync(user.Id, cancelled.Id));

        var first = await AddSessionAsync(f, TimeSpan.FromDays(2));
        var second = await AddSessionAsync(f, TimeSpan.FromDays(2) + TimeSpan.FromMinutes(30));
        await f.Bookings.BookSessionAsync(user.Id, first.Id);
        await AssertFlowAsync(409, () => f.Bookings.BookSessionAsync(user.Id, second.Id));
    }

    [Fact]
    public async Task Book_FullSession_JoinsWaitlist_AndCapacityIsNeverExceeded()
    {
        var f = await CreateFixtureAsync(classCapacity: 1);
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (u1, _) = await AddMemberAsync(f, "first");
        var (u2, _) = await AddMemberAsync(f, "second");

        Assert.True((await f.Bookings.BookSessionAsync(u1.Id, session.Id)).Booked);
        var second = await f.Bookings.BookSessionAsync(u2.Id, session.Id);

        Assert.False(second.Booked);
        Assert.True(second.Waitlisted);
        Assert.Equal(1, await f.Db.SessionBookings.CountAsync(b => b.Status == "Booked"));
        Assert.Equal(1, await f.Db.ClassWaitlists.CountAsync(w => w.Status == "Waiting"));
    }

    // ---------------------------------------------------------------- UC-44

    [Fact]
    public async Task Cancel_OnTime_PromotesFirstWaitlistedMember()
    {
        var f = await CreateFixtureAsync(classCapacity: 1);
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (u1, _) = await AddMemberAsync(f, "holder");
        var (u2, m2) = await AddMemberAsync(f, "waiter");
        var booked = await f.Bookings.BookSessionAsync(u1.Id, session.Id);
        await f.Bookings.BookSessionAsync(u2.Id, session.Id);

        var result = await f.Bookings.CancelBookingAsync(u1.Id, booked.Booking!.Id);

        Assert.False(result.IsLateCancellation);
        Assert.Equal("Cancelled", result.Booking.Status);
        Assert.Equal(m2.Id, result.PromotedMemberId);
        Assert.Equal("Booked", (await f.Db.SessionBookings.SingleAsync(b => b.MemberId == m2.Id)).Status);
        Assert.Equal("Promoted", (await f.Db.ClassWaitlists.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cancel_WithEmptyWaitlist_ReleasesSeatQuietly()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (user, _) = await AddMemberAsync(f, "solo");
        var booked = await f.Bookings.BookSessionAsync(user.Id, session.Id);

        var result = await f.Bookings.CancelBookingAsync(user.Id, booked.Booking!.Id);
        Assert.Null(result.PromotedMemberId);

        // Re-booking after cancellation re-uses the unique (session, member) row.
        var again = await f.Bookings.BookSessionAsync(user.Id, session.Id);
        Assert.True(again.Booked);
        Assert.Equal(1, await f.Db.SessionBookings.CountAsync());
    }

    [Fact]
    public async Task Cancel_Late_IsChargedAndDoesNotPromote()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromHours(1));
        var (user, _) = await AddMemberAsync(f, "late");
        f.Db.SessionBookings.Add(new SessionBooking { SessionId = session.Id, MemberId = (await f.Db.MemberProfiles.SingleAsync(m => m.UserId == user.Id)).Id, BookedAt = DateTime.UtcNow, Status = "Booked" });
        await f.Db.SaveChangesAsync();
        var bookingId = (await f.Db.SessionBookings.SingleAsync()).Id;

        var result = await f.Bookings.CancelBookingAsync(user.Id, bookingId);

        Assert.True(result.IsLateCancellation);
        Assert.Equal("CANCELLED_LATE_CHARGED", result.Booking.Status);
    }

    [Fact]
    public async Task LegacyClassEnrollment_LateCancellationAlsoKeepsNextSessionCharged()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromHours(1));
        var (_, member) = await AddMemberAsync(f, "legacy-late-cancel");
        var enrollment = new ClassEnrollment
        {
            ClassId = f.Class.Id, MemberId = member.Id, SubscriptionId = 1,
            RegisteredAt = DateTime.UtcNow, Status = "Confirmed"
        };
        f.Db.ClassEnrollments.Add(enrollment);
        await f.Db.SaveChangesAsync();

        var cancelled = await new CoreFlowService(new UnitOfWork(f.Db))
            .CancelClassEnrollmentAsync(f.Class.Id, member.Id, "test");

        Assert.Equal("CancelledLateCharged", cancelled.Status);
        Assert.Contains("remains charged", cancelled.CancellationReason);
        Assert.Equal(session.Id, (await f.Db.ClassSessions.SingleAsync()).Id);
    }

    [Fact]
    public async Task Cancel_Fails_ForOthersAlreadyCancelledAndPastSessions()
    {
        var f = await CreateFixtureAsync();
        var session = await AddSessionAsync(f, TimeSpan.FromDays(1));
        var (owner, ownerMember) = await AddMemberAsync(f, "owner");
        var (stranger, _) = await AddMemberAsync(f, "stranger");
        var booked = await f.Bookings.BookSessionAsync(owner.Id, session.Id);

        await AssertFlowAsync(403, () => f.Bookings.CancelBookingAsync(stranger.Id, booked.Booking!.Id));
        await f.Bookings.CancelBookingAsync(owner.Id, booked.Booking!.Id);
        await AssertFlowAsync(409, () => f.Bookings.CancelBookingAsync(owner.Id, booked.Booking!.Id));

        var past = await AddSessionAsync(f, TimeSpan.FromHours(-3));
        var pastBooking = new SessionBooking { SessionId = past.Id, MemberId = ownerMember.Id, BookedAt = DateTime.UtcNow, Status = "Booked" };
        f.Db.SessionBookings.Add(pastBooking);
        await f.Db.SaveChangesAsync();
        await AssertFlowAsync(400, () => f.Bookings.CancelBookingAsync(owner.Id, pastBooking.Id));
    }
}

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
        await AssertFlowAsync(400, () => f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null, 0, 20)));
        await AssertFlowAsync(400, () => f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null, 1, 0)));

        var empty = await f.Queries.GetSessionsAsync(new SessionScheduleQuery(f.Sport.Id + 100, null, null, null));
        Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task GetSessions_ShowsCancelledClassAsNotBookable()
    {
        var f = await CreateFixtureAsync();
        await AddSessionAsync(f, TimeSpan.FromDays(1));
        f.Class.Status = "Cancelled";
        await f.Db.SaveChangesAsync();

        var page = await f.Queries.GetSessionsAsync(new SessionScheduleQuery(null, null, null, null));
        var item = Assert.Single(page.Items);
        Assert.Equal("Cancelled", item.ClassStatus);
        Assert.False(item.IsBookable);
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

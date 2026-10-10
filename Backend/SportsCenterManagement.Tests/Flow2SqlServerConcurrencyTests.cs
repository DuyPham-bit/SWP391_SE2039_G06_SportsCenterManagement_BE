using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

namespace SportsCenterManagement.Tests;

public sealed class Flow2SqlServerConcurrencyTests
{
    [Fact]
    public async Task ConcurrentLastSeatRequests_AreSerializedBySqlServer()
    {
        var template = Environment.GetEnvironmentVariable("SCMS_SQLSERVER_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(template)) return;

        var options = new DbContextOptionsBuilder<SportsCenterDbContext>().UseSqlServer(template).Options;
        long user1Id = 0;
        long user2Id = 0;
        long member1Id = 0;
        long member2Id = 0;
        long classId = 0;
        long sessionId = 0;
        long sportId = 0;

        try
        {
            await using (var setup = new SportsCenterDbContext(options))
            {
                Assert.True(await setup.Database.CanConnectAsync(), "SCMS_SQLSERVER_TEST_CONNECTION must point to an existing test database.");
                Assert.Empty(await setup.Database.GetPendingMigrationsAsync());
                var center = await setup.Centers.FirstAsync();
                var suffix = Guid.NewGuid().ToString("N");
                var sport = new Sport { Name = $"Race {suffix}", Status = "Active" };
                setup.Sports.Add(sport);
                await setup.SaveChangesAsync();
                sportId = sport.Id;
                var package = await setup.MembershipPackages.FirstAsync(item => item.CenterId == center.Id && item.Status == "Active");
                var memberRole = await setup.Roles.SingleAsync(item => item.Name == "Member");
                var user1 = new User
                {
                    RoleId = memberRole.Id, Username = $"race1_{suffix}", Email = $"race1_{suffix}@test.invalid",
                    PasswordHash = "not-used", Status = "Active", CreatedAt = DateTime.UtcNow
                };
                var user2 = new User
                {
                    RoleId = memberRole.Id, Username = $"race2_{suffix}", Email = $"race2_{suffix}@test.invalid",
                    PasswordHash = "not-used", Status = "Active", CreatedAt = DateTime.UtcNow
                };
                setup.Users.AddRange(user1, user2);
                await setup.SaveChangesAsync();
                user1Id = user1.Id;
                user2Id = user2.Id;

                var member1 = new MemberProfile { UserId = user1.Id, MemberCode = $"R1-{suffix}", FullName = "Race One", CreatedAt = DateTime.UtcNow };
                var member2 = new MemberProfile { UserId = user2.Id, MemberCode = $"R2-{suffix}", FullName = "Race Two", CreatedAt = DateTime.UtcNow };
                setup.MemberProfiles.AddRange(member1, member2);
                await setup.SaveChangesAsync();
                member1Id = member1.Id;
                member2Id = member2.Id;
                var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
                setup.MemberSubscriptions.AddRange(
                    new MemberSubscription { MemberId = member1.Id, PackageId = package.Id, Status = "Active", Price = package.Price, StartDate = today.AddDays(-1), EndDate = today.AddDays(10), CreatedAt = DateTime.UtcNow },
                    new MemberSubscription { MemberId = member2.Id, PackageId = package.Id, Status = "Active", Price = package.Price, StartDate = today.AddDays(-1), EndDate = today.AddDays(10), CreatedAt = DateTime.UtcNow });
                var classEntity = new ClassEntity
                {
                    CenterId = center.Id, SportId = sport.Id, Name = $"Race {suffix}", Capacity = 1,
                    DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow
                };
                setup.Classes.Add(classEntity);
                await setup.SaveChangesAsync();
                classId = classEntity.Id;
                var session = new ClassSession
                {
                    ClassId = classEntity.Id,
                    SessionDate = today.AddDays(2),
                    StartTime = new TimeOnly(10, 0),
                    EndTime = new TimeOnly(11, 0),
                    SessionStatus = "Scheduled",
                    CreatedAt = DateTime.UtcNow
                };
                setup.ClassSessions.Add(session);
                await setup.SaveChangesAsync();
                sessionId = session.Id;
            }

            BookSessionResult result1;
            BookSessionResult result2;
            await using (var db1 = new SportsCenterDbContext(options))
            await using (var db2 = new SportsCenterDbContext(options))
            {
                var service1 = new SessionBookingService(new UnitOfWork(db1));
                var service2 = new SessionBookingService(new UnitOfWork(db2));
                var results = await Task.WhenAll(
                    service1.BookSessionAsync(user1Id, sessionId),
                    service2.BookSessionAsync(user2Id, sessionId));
                result1 = results[0];
                result2 = results[1];
            }

            Assert.Single(new[] { result1, result2 }, result => result.Booked);
            Assert.Single(new[] { result1, result2 }, result => result.Waitlisted);
            var waitlistedUserId = result1.Waitlisted ? user1Id : user2Id;
            await using (var db3 = new SportsCenterDbContext(options))
            await using (var db4 = new SportsCenterDbContext(options))
            {
                var service3 = new SessionBookingService(new UnitOfWork(db3));
                var service4 = new SessionBookingService(new UnitOfWork(db4));
                var duplicateWaitlistRequests = await Task.WhenAll(
                    service3.BookSessionAsync(waitlistedUserId, sessionId),
                    service4.BookSessionAsync(waitlistedUserId, sessionId));
                Assert.All(duplicateWaitlistRequests, result => Assert.True(result.Waitlisted));
                Assert.Single(duplicateWaitlistRequests.Select(result => result.WaitlistId).Distinct());
            }

            await using var verify = new SportsCenterDbContext(options);
            Assert.Equal(1, await verify.SessionBookings.CountAsync(item => item.SessionId == sessionId && item.Status == "Booked"));
            Assert.Equal(1, await verify.ClassWaitlists.CountAsync(item => item.SessionId == sessionId && item.Status == "Waiting"));
        }
        finally
        {
            await using var cleanup = new SportsCenterDbContext(options);
            if (sessionId > 0)
            {
                await cleanup.SessionBookings.Where(item => item.SessionId == sessionId).ExecuteDeleteAsync();
                await cleanup.ClassWaitlists.Where(item => item.SessionId == sessionId).ExecuteDeleteAsync();
                await cleanup.ClassSessions.Where(item => item.Id == sessionId).ExecuteDeleteAsync();
            }
            if (classId > 0)
                await cleanup.Classes.Where(item => item.Id == classId).ExecuteDeleteAsync();
            if (sportId > 0)
                await cleanup.Sports.Where(item => item.Id == sportId).ExecuteDeleteAsync();
            var memberIds = new[] { member1Id, member2Id }.Where(id => id > 0).ToArray();
            if (memberIds.Length > 0)
            {
                await cleanup.MemberSubscriptions.Where(item => memberIds.Contains(item.MemberId)).ExecuteDeleteAsync();
                await cleanup.MemberProfiles.Where(item => memberIds.Contains(item.Id)).ExecuteDeleteAsync();
            }
            var userIds = new[] { user1Id, user2Id }.Where(id => id > 0).ToArray();
            if (userIds.Length > 0)
                await cleanup.Users.Where(item => userIds.Contains(item.Id)).ExecuteDeleteAsync();
        }
    }
}

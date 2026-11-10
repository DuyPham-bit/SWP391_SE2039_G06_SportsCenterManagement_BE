using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

/// <summary>Class schedules follow each center's configured opening hours (fallback 05:00–22:00).</summary>
static class CenterHoursChecks
{
    public static async Task Run(SportsCenterDbContext db, long actorUserId, Func<string, Func<Task<bool>>, Task> check)
    {
        var configured = new Center { Name = "Hours 06-21", Address = "Audit", Status = "Active", CreatedAt = DateTime.UtcNow,
            OpeningTime = new TimeOnly(6, 0), ClosingTime = new TimeOnly(21, 0) };
        var unconfigured = new Center { Name = "Hours default", Address = "Audit", Status = "Active", CreatedAt = DateTime.UtcNow };
        var sport = new Sport { Name = "Hours sport", Status = "Active" };
        db.AddRange(configured, unconfigured, sport);
        await db.SaveChangesAsync();
        ClassEntity NewClass(long centerId, string name) => new() { CenterId = centerId, SportId = sport.Id, Name = name,
            Capacity = 10, DurationMinutes = 90, Status = "Draft", CreatedAt = DateTime.UtcNow };
        var limited = NewClass(configured.Id, "Hours limited class");
        var fallback = NewClass(unconfigured.Id, "Hours fallback class");
        db.AddRange(limited, fallback);
        await db.SaveChangesAsync();

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(60);
        async Task<string?> Create(ClassEntity cls, int day, TimeOnly start, TimeOnly end)
        {
            try
            {
                await new ClassManagementService(new UnitOfWork(db)).CreateScheduleAsync(actorUserId, cls.Id,
                    new CreateClassScheduleRequest(null, day, start, end, from, from.AddDays(13)));
                return null;
            }
            catch (InvalidOperationException error) { return error.Message; }
        }

        await check("schedule before the center's configured opening time is rejected with its real hours", async () =>
        {
            var early = await Create(limited, 1, new TimeOnly(5, 30), new TimeOnly(6, 30));
            var late = await Create(limited, 2, new TimeOnly(20, 30), new TimeOnly(21, 30));
            return early is not null && early.Contains("06:00") && early.Contains("21:00") && late is not null;
        });

        await check("schedule inside the center's configured hours is accepted", async () =>
            await Create(limited, 3, new TimeOnly(7, 0), new TimeOnly(8, 0)) is null);

        await check("a center without configured hours keeps the 05:00-22:00 default", async () =>
            await Create(fallback, 4, new TimeOnly(5, 0), new TimeOnly(6, 0)) is null
            && await Create(fallback, 5, new TimeOnly(21, 30), new TimeOnly(22, 30)) is not null);
    }
}

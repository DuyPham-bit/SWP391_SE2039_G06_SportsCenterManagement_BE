using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.Tests;

public class CoachSessionRulesTests
{
    private const long Me = 1, Other = 2;
    private static readonly DateOnly Today = new(2026, 11, 15);
    private static readonly TimeOnly Noon = new(12, 0);

    [Fact]
    public async Task Taught_IncludesSubstitutionsAndExcludesHandedOffOrCancelled()
    {
        using var db = TestDb.Create();
        var mine = Class(db, "Published");
        var others = Class(db, "Published");
        var cancelled = Class(db, FlowStatuses.ClassCancelled);
        db.AddRange(
            new ClassCoach { ClassId = mine.Id, CoachId = Me, IsPrimary = true },
            new ClassCoach { ClassId = cancelled.Id, CoachId = Me, IsPrimary = true },
            new ClassCoach { ClassId = others.Id, CoachId = Other, IsPrimary = true });
        db.SaveChanges();
        var own = Session(db, mine, null, Today.AddDays(-1));
        Session(db, mine, Other, Today.AddDays(-1));                  // handed to a substitute
        var substitution = Session(db, others, Me, Today.AddDays(-1)); // taken over by me
        Session(db, cancelled, null, Today.AddDays(-1));               // class cancelled
        Session(db, mine, null, Today.AddDays(-1), FlowStatuses.SessionCancelled);

        var taught = await CoachSessionRules.Taught(db, Me).Select(s => s.Id).OrderBy(id => id).ToListAsync();

        Assert.Equal(new[] { own.Id, substitution.Id }, taught);
    }

    [Fact]
    public async Task Taught_AssistantKeepsSessionsStampedWithPrimary_PrimaryLosesHandedOffOnes()
    {
        const long Assistant = 3, Substitute = 4;
        using var db = TestDb.Create();
        var cls = Class(db, "Published");
        db.AddRange(
            new ClassCoach { ClassId = cls.Id, CoachId = Me, IsPrimary = true },
            new ClassCoach { ClassId = cls.Id, CoachId = Assistant, IsPrimary = false });
        db.SaveChanges();
        var stamped = Session(db, cls, Me, Today.AddDays(-1));          // generated: CoachId = primary
        var handedOff = Session(db, cls, Substitute, Today.AddDays(-2)); // primary on leave, substitute leads

        var assistant = await CoachSessionRules.Taught(db, Assistant).Select(s => s.Id).OrderBy(id => id).ToListAsync();
        var primary = await CoachSessionRules.Taught(db, Me).Select(s => s.Id).ToListAsync();

        Assert.Equal(new[] { stamped.Id, handedOff.Id }.OrderBy(id => id), assistant);
        Assert.Equal(new[] { stamped.Id }, primary);
        Assert.True(CoachSessionRules.Teaches(Me, Assistant, assignmentIsPrimary: false));
        Assert.False(CoachSessionRules.Teaches(Substitute, Me, assignmentIsPrimary: true));
    }

    [Fact]
    public async Task Completed_CountsOnlySessionsWhoseEndHasPassed()
    {
        using var db = TestDb.Create();
        var cls = Class(db, "Published");
        db.Add(new ClassCoach { ClassId = cls.Id, CoachId = Me, IsPrimary = true });
        db.SaveChanges();
        Session(db, cls, null, Today.AddDays(-1));                                   // yesterday: done
        Session(db, cls, null, Today, start: new TimeOnly(8, 0), end: new TimeOnly(9, 0));   // ended this morning
        Session(db, cls, null, Today, start: new TimeOnly(18, 0), end: new TimeOnly(19, 0)); // later today
        Session(db, cls, null, Today, start: new TimeOnly(23, 0), end: new TimeOnly(0, 30)); // ends after midnight

        var completed = await CoachSessionRules.Taught(db, Me).Completed(Today, Noon).CountAsync();

        Assert.Equal(2, completed);
    }

    private static ClassEntity Class(SportsCenterDbContext db, string status)
    {
        var cls = new ClassEntity
        {
            CenterId = 1, SportId = 1, Name = "C", Capacity = 10, DurationMinutes = 60, Status = status, CreatedAt = TestDb.Now
        };
        db.Add(cls);
        db.SaveChanges();
        return cls;
    }

    private static ClassSession Session(SportsCenterDbContext db, ClassEntity cls, long? coachId, DateOnly date,
        string status = FlowStatuses.SessionScheduled, TimeOnly? start = null, TimeOnly? end = null)
    {
        var session = new ClassSession
        {
            ClassId = cls.Id, CoachId = coachId, SessionDate = date, StartTime = start ?? new TimeOnly(7, 0),
            EndTime = end ?? new TimeOnly(8, 0), SessionStatus = status, CreatedAt = TestDb.Now
        };
        db.Add(session);
        db.SaveChanges();
        return session;
    }
}

using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Single definition of "sessions a coach taught" shared by the coach KPI and payroll, so the
/// numbers a coach sees match what they are paid for.
/// </summary>
internal static class CoachSessionRules
{
    /// <summary>
    /// Sessions the coach teaches. <c>session.CoachId</c> holds the session's lead coach (generated as the primary
    /// coach, replaced by a substitute). Assistant coaches (IsPrimary = false) always teach their class's sessions;
    /// the primary coach teaches unless the session was handed to someone else. Cancelled sessions/classes excluded.
    /// Keep this rule in sync with the inline copies in ClassScheduleQueryService, ClassRepository and TrainingService.
    /// </summary>
    public static IQueryable<ClassSession> Taught(SportsCenterDbContext db, long coachId) =>
        from session in db.ClassSessions
        join cls in db.Classes on session.ClassId equals cls.Id
        where session.SessionStatus != FlowStatuses.SessionCancelled && cls.Status != FlowStatuses.ClassCancelled
            && (session.CoachId == coachId
                || db.ClassCoaches.Any(cc => cc.ClassId == session.ClassId && cc.CoachId == coachId
                    && (!cc.IsPrimary || session.CoachId == null)))
        select session;

    /// <summary>In-memory form of the same rule (for already loaded sessions and assignments).</summary>
    public static bool Teaches(long? sessionCoachId, long coachId, bool? assignmentIsPrimary) =>
        sessionCoachId == coachId
        || (assignmentIsPrimary is { } isPrimary && (!isPrimary || sessionCoachId == null));

    /// <summary>
    /// A session counts as completed once its end time has passed (Vietnam time). Nothing moves sessions to a
    /// "Completed" status, so time is the source of truth. A session ending after midnight completes the next day.
    /// </summary>
    public static IQueryable<ClassSession> Completed(this IQueryable<ClassSession> sessions, DateOnly today, TimeOnly now) =>
        sessions.Where(s => s.SessionDate < today
            || (s.SessionDate == today && s.EndTime > s.StartTime && s.EndTime <= now));

    public static bool IsCompleted(ClassSession s, DateOnly today, TimeOnly now) =>
        s.SessionDate < today || (s.SessionDate == today && s.EndTime > s.StartTime && s.EndTime <= now);
}

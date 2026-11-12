using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.DAL.Repositories.Implementations;

public sealed class ClassRepository(SportsCenterDbContext db) : IClassRepository
{
    public async Task<long?> GetMemberIdByUserIdAsync(long userId, CancellationToken ct = default)
    {
        return await db.MemberProfiles
            .Where(x => x.UserId == userId)
            .Select(x => (long?)x.Id)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MemberEnrollmentItem>> GetMemberEnrollmentsAsync(long memberId, CancellationToken ct = default)
    {
        return await (
            from enrollment in db.ClassEnrollments
            join classEntity in db.Classes on enrollment.ClassId equals classEntity.Id
            where enrollment.MemberId == memberId
            orderby enrollment.RegisteredAt descending
            select new MemberEnrollmentItem(
                enrollment.Id,
                enrollment.ClassId,
                classEntity.Name,
                enrollment.SubscriptionId,
                enrollment.RegisteredAt,
                enrollment.CancelledAt,
                enrollment.Status,
                enrollment.CancellationReason)
        ).ToListAsync(ct);
    }

    public async Task<bool> ExistsEnrollmentAsync(long enrollmentId, long classId, long memberId, CancellationToken ct = default)
    {
        return await db.ClassEnrollments
            .AnyAsync(x => x.Id == enrollmentId && x.ClassId == classId && x.MemberId == memberId, ct);
    }

    public async Task<long?> GetClassIdBySessionIdAsync(long sessionId, CancellationToken ct = default)
    {
        return await db.ClassSessions
            .Where(s => s.Id == sessionId)
            .Select(s => (long?)s.ClassId)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<long?> GetClassIdByBookingIdAsync(long bookingId, long memberId, CancellationToken ct = default)
    {
        return await (
            from b in db.SessionBookings
            join s in db.ClassSessions on b.SessionId equals s.Id
            where b.Id == bookingId && b.MemberId == memberId
            select (long?)s.ClassId
        ).SingleOrDefaultAsync(ct);
    }

    public async Task<(long UserId, long? CenterId)?> GetMemberUserAndCenterAsync(long memberId, CancellationToken ct = default)
    {
        var row = await db.MemberProfiles
            .Where(m => m.Id == memberId)
            .Select(m => new { m.UserId, m.CenterId })
            .SingleOrDefaultAsync(ct);

        return row is null ? null : (row.UserId, row.CenterId);
    }

    public async Task<IReadOnlyList<MemberWaitlistItem>> GetMemberWaitlistAsync(long userId, CancellationToken ct = default)
    {
        return await (
            from w in db.ClassWaitlists
            join m in db.MemberProfiles on w.MemberId equals m.Id
            join s in db.ClassSessions on w.SessionId equals s.Id
            join c in db.Classes on s.ClassId equals c.Id
            where m.UserId == userId && w.Status == "Waiting"
            orderby s.SessionDate, s.StartTime
            select new MemberWaitlistItem(
                w.Id,
                w.SessionId,
                c.Name,
                s.SessionDate,
                s.StartTime,
                s.EndTime,
                w.Status,
                w.JoinedAt,
                s.SessionStatus,
                // FIFO position: same ordering the promotion uses (JoinedAt, then Id).
                db.ClassWaitlists.Count(other => other.SessionId == w.SessionId && other.Status == "Waiting"
                    && (other.JoinedAt < w.JoinedAt || (other.JoinedAt == w.JoinedAt && other.Id < w.Id))) + 1)
        ).ToListAsync(ct);
    }

    public async Task<int> CancelWaitlistEntryAsync(long waitlistId, long memberId, CancellationToken ct = default)
    {
        var entry = await db.ClassWaitlists
            .SingleOrDefaultAsync(w => w.Id == waitlistId && w.MemberId == memberId, ct);

        if (entry is null) return 0; // Not found
        if (entry.Status != "Waiting") return -1; // Conflict

        entry.Status = "Cancelled";
        await db.SaveChangesAsync(ct);
        return 1;
    }

    public async Task<bool> IsCoachAssignedToSessionAsync(long coachUserId, long sessionId, CancellationToken ct = default)
    {
        return await (
            from s in db.ClassSessions
            join c in db.Classes on s.ClassId equals c.Id
            join coach in db.CoachProfiles on c.CenterId equals coach.CenterId
            where s.Id == sessionId && coach.UserId == coachUserId && coach.Status == "Active" &&
                (s.CoachId == coach.Id || (s.CoachId == null &&
                    db.ClassCoaches.Any(a => a.ClassId == c.Id && a.CoachId == coach.Id)))
            select s.Id
        ).AnyAsync(ct);
    }

    public async Task<IReadOnlyList<SessionRosterItem>> GetSessionRosterAsync(long sessionId, CancellationToken ct = default)
    {
        var classId = await db.ClassSessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.ClassId)
            .SingleOrDefaultAsync(ct);

        return await (
            from m in db.MemberProfiles
            join u in db.Users on m.UserId equals u.Id
            where u.Status == "Active" &&
                (db.SessionBookings.Any(b => b.SessionId == sessionId && b.MemberId == m.Id && b.Status == "Booked") ||
                 db.ClassEnrollments.Any(e => e.ClassId == classId && e.MemberId == m.Id &&
                    (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")))
            orderby m.FullName
            select new SessionRosterItem(
                db.SessionBookings.Where(b => b.SessionId == sessionId && b.MemberId == m.Id && b.Status == "Booked")
                    .Select(b => (long?)b.Id).FirstOrDefault(),
                m.Id,
                m.FullName,
                m.MemberCode,
                u.Email,
                u.Phone,
                m.FitnessGoal,
                m.FitnessLevel,
                db.Attendances.Where(a => a.SessionId == sessionId && a.MemberId == m.Id).Select(a => a.AttendanceStatus).FirstOrDefault(),
                db.Attendances.Where(a => a.SessionId == sessionId && a.MemberId == m.Id).Select(a => a.Note).FirstOrDefault())
        ).ToListAsync(ct);
    }

    public async Task<ClassSession?> GetSessionByIdAsync(long sessionId, CancellationToken ct = default)
    {
        return await db.ClassSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task<long?> GetCenterIdByClassIdAsync(long classId, CancellationToken ct = default)
    {
        return await db.Classes.Where(c => c.Id == classId).Select(c => (long?)c.CenterId).SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<long>> GetEligibleAttendanceMemberIdsAsync(long sessionId, long classId, IEnumerable<long> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToArray();
        return await db.MemberProfiles.Where(m => ids.Contains(m.Id) &&
            db.Users.Any(u => u.Id == m.UserId && u.Status == "Active") &&
            (db.SessionBookings.Any(b => b.SessionId == sessionId && b.MemberId == m.Id && b.Status == "Booked") ||
             db.ClassEnrollments.Any(e => e.ClassId == classId && e.MemberId == m.Id &&
                (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled"))))
            .Select(m => m.Id).ToListAsync(ct);
    }

    public async Task UpsertAttendancesAsync(long sessionId, long actorUserId, IReadOnlyList<(long MemberId, string Status, string? Note)> records, CancellationToken ct = default)
    {
        var ids = records.Select(r => r.MemberId).ToArray();
        var existing = await db.Attendances
            .Where(a => a.SessionId == sessionId && ids.Contains(a.MemberId))
            .ToDictionaryAsync(a => a.MemberId, ct);

        foreach (var r in records)
        {
            if (!existing.TryGetValue(r.MemberId, out var row))
            {
                row = new Attendance { SessionId = sessionId, MemberId = r.MemberId, CreatedAt = DateTime.UtcNow };
                db.Attendances.Add(row);
            }
            row.AttendanceStatus = r.Status;
            row.Note = r.Note;
            row.CheckedInBy = actorUserId;
            row.CheckInTime = r.Status is "PRESENT" or "LATE" ? row.CheckInTime ?? DateTime.UtcNow : null;
        }

        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
public sealed class BookingOperationsController(SportsCenterDbContext db, ISessionBookingService bookings) : FlowControllerBase, IAsyncActionFilter
{
    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [HttpGet("members/{memberId:long}/session-bookings")]
    [CenterCapability("lookup_member")]
    public async Task<IActionResult> ListForMember(long memberId, CancellationToken ct) =>
        Ok(await bookings.ListMyBookingsAsync(await MemberUser(memberId, ct), ct));

    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [HttpPost("members/{memberId:long}/class-sessions/{sessionId:long}/bookings")]
    [CenterCapability("lookup_member")]
    public async Task<IActionResult> BookForMember(long memberId, long sessionId, CancellationToken ct)
    {
        var userId = await MemberUser(memberId, ct);
        var classId = await db.ClassSessions.Where(s => s.Id == sessionId).Select(s => (long?)s.ClassId).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy buổi học.") { Data = { ["StatusCode"] = 404 } };
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        var result = await bookings.BookSessionAsync(userId, sessionId, ct, CurrentUserId);
        return StatusCode(result.Booked ? 201 : 202, result);
    }

    [Authorize(Roles = "RECEPTIONIST,MANAGER,ADMIN")]
    [HttpDelete("members/{memberId:long}/session-bookings/{bookingId:long}")]
    [CenterCapability("lookup_member")]
    public async Task<IActionResult> CancelForMember(long memberId, long bookingId, CancellationToken ct)
    {
        var userId = await MemberUser(memberId, ct);
        var classId = await (from b in db.SessionBookings join s in db.ClassSessions on b.SessionId equals s.Id
            where b.Id == bookingId && b.MemberId == memberId select (long?)s.ClassId).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt đặt của hội viên.") { Data = { ["StatusCode"] = 404 } };
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await bookings.CancelBookingAsync(userId, bookingId, ct, CurrentUserId));
    }

    [Authorize(Roles = "MEMBER")]
    [HttpGet("members/me/session-waitlist")]
    [CenterCapability("book_class")]
    public async Task<IActionResult> Waitlist(CancellationToken ct) => Ok(await (
        from w in db.ClassWaitlists join m in db.MemberProfiles on w.MemberId equals m.Id
        join s in db.ClassSessions on w.SessionId equals s.Id join c in db.Classes on s.ClassId equals c.Id
        where m.UserId == CurrentUserId && w.Status == "Waiting"
        orderby s.SessionDate, s.StartTime
        select new { w.Id, w.SessionId, ClassName = c.Name, s.SessionDate, s.StartTime, s.EndTime, w.Status, w.JoinedAt }).ToListAsync(ct));

    [Authorize(Roles = "MEMBER")]
    [HttpDelete("members/me/session-waitlist/{waitlistId:long}")]
    [CenterCapability("cancel_booking")]
    public async Task<IActionResult> LeaveWaitlist(long waitlistId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var memberId = await db.MemberProfiles.Where(m => m.UserId == CurrentUserId).Select(m => m.Id).SingleAsync(ct);
        var entry = await db.ClassWaitlists.SingleOrDefaultAsync(w => w.Id == waitlistId && w.MemberId == memberId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt chờ.") { Data = { ["StatusCode"] = 404 } };
        if (entry.Status != "Waiting") throw new InvalidOperationException("Lượt chờ đã được xử lý, vui lòng tải lại lịch.") { Data = { ["StatusCode"] = 409 } };
        entry.Status = "Cancelled";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = "COACH")]
    [HttpGet("coaches/me/sessions/{sessionId:long}/members")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Roster(long sessionId, CancellationToken ct)
    {
        await EnsureCoach(sessionId, ct);
        var classId = await db.ClassSessions.Where(s => s.Id == sessionId).Select(s => s.ClassId).SingleAsync(ct);
        return Ok(await (from m in db.MemberProfiles
            join u in db.Users on m.UserId equals u.Id
            where u.Status == "Active" &&
                (db.SessionBookings.Any(b => b.SessionId == sessionId && b.MemberId == m.Id && b.Status == "Booked") ||
                 db.ClassEnrollments.Any(e => e.ClassId == classId && e.MemberId == m.Id &&
                    (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")))
            orderby m.FullName
            select new {
                BookingId = db.SessionBookings.Where(b => b.SessionId == sessionId && b.MemberId == m.Id && b.Status == "Booked")
                    .Select(b => (long?)b.Id).FirstOrDefault(),
                MemberId = m.Id, MemberName = m.FullName, m.MemberCode,
                u.Email, u.Phone, m.FitnessGoal, m.FitnessLevel,
                AttendanceStatus = db.Attendances.Where(a => a.SessionId == sessionId && a.MemberId == m.Id).Select(a => a.AttendanceStatus).FirstOrDefault(),
                Note = db.Attendances.Where(a => a.SessionId == sessionId && a.MemberId == m.Id).Select(a => a.Note).FirstOrDefault()
            }).ToListAsync(ct));
    }

    public sealed record AttendanceItem(long MemberId, string Status, string? Note);

    [Authorize(Roles = "COACH")]
    [HttpPut("coaches/me/sessions/{sessionId:long}/attendance")]
    [CenterCapability("take_attendance")]
    public async Task<IActionResult> Attendance(long sessionId, AttendanceItem[] records, CancellationToken ct)
    {
        if (records.Length == 0 || records.Length > 1000 || records.Select(r => r.MemberId).Distinct().Count() != records.Length
            || records.Any(r => r.Status is not ("PRESENT" or "ABSENT" or "LATE" or "EXCUSED") || r.Note?.Length > 500))
            throw new InvalidOperationException("Danh sách điểm danh không hợp lệ.") { Data = { ["StatusCode"] = 400 } };
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await EnsureCoach(sessionId, ct);
        var session = await db.ClassSessions.SingleAsync(s => s.Id == sessionId, ct);
        var vietnamNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
        if (string.Equals(session.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ điểm danh khi buổi học đã bắt đầu và không bị hủy.") { Data = { ["StatusCode"] = 409 } };
        if (session.SessionDate < DateOnly.FromDateTime(vietnamNow))
            throw new InvalidOperationException("Buổi học đã qua ngày nên không thể sửa điểm danh.") { Data = { ["StatusCode"] = 409 } };
        if (session.SessionDate.ToDateTime(session.StartTime) > vietnamNow)
            throw new InvalidOperationException("Chỉ điểm danh khi buổi học đã bắt đầu và không bị hủy.") { Data = { ["StatusCode"] = 409 } };
        var ids = records.Select(r => r.MemberId).ToArray();
        var eligible = await db.MemberProfiles.Where(m => ids.Contains(m.Id) &&
            db.Users.Any(u => u.Id == m.UserId && u.Status == "Active") &&
            (db.SessionBookings.Any(b => b.SessionId == sessionId && b.MemberId == m.Id && b.Status == "Booked") ||
             db.ClassEnrollments.Any(e => e.ClassId == session.ClassId && e.MemberId == m.Id &&
                (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled"))))
            .Select(m => m.Id).ToListAsync(ct);
        if (eligible.Count != records.Length) throw new InvalidOperationException("Có học viên không đăng ký buổi học này.") { Data = { ["StatusCode"] = 400 } };
        var existing = await db.Attendances.Where(a => a.SessionId == sessionId && ids.Contains(a.MemberId)).ToDictionaryAsync(a => a.MemberId, ct);
        foreach (var r in records)
        {
            if (!existing.TryGetValue(r.MemberId, out var row))
            {
                row = new Attendance { SessionId = sessionId, MemberId = r.MemberId, CreatedAt = DateTime.UtcNow };
                db.Attendances.Add(row);
            }
            row.AttendanceStatus = r.Status; row.Note = r.Note; row.CheckedInBy = CurrentUserId;
            row.CheckInTime = r.Status is "PRESENT" or "LATE" ? row.CheckInTime ?? DateTime.UtcNow : null;
        }
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = await db.Classes.Where(c => c.Id == session.ClassId).Select(c => c.CenterId).SingleAsync(ct),
            Action = "SESSION_ATTENDANCE", EntityType = "ClassSession", EntityId = sessionId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(new { Count = records.Length });
    }

    private async Task<long> MemberUser(long memberId, CancellationToken ct)
    {
        var member = await db.MemberProfiles.SingleOrDefaultAsync(m => m.Id == memberId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy hội viên.") { Data = { ["StatusCode"] = 404 } };
        if (member.CenterId is not { } centerId) throw new InvalidOperationException("Hội viên chưa được gán trung tâm.") { Data = { ["StatusCode"] = 403 } };
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return member.UserId;
    }

    private async Task EnsureCoach(long sessionId, CancellationToken ct)
    {
        if (!await (from s in db.ClassSessions
            join c in db.Classes on s.ClassId equals c.Id
            join coach in db.CoachProfiles on c.CenterId equals coach.CenterId
            where s.Id == sessionId && coach.UserId == CurrentUserId && coach.Status == "Active" &&
                (s.CoachId == coach.Id || (s.CoachId == null &&
                    db.ClassCoaches.Any(a => a.ClassId == c.Id && a.CoachId == coach.Id)))
            select s.Id).AnyAsync(ct))
            throw new InvalidOperationException("Bạn không được phân công phụ trách buổi học này.") { Data = { ["StatusCode"] = 403 } };
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        // Only tagged business failures are mapped here; unexpected errors remain available to the middleware.
        if (executed.ExceptionHandled || executed.Exception is not InvalidOperationException exception ||
            exception.Data["StatusCode"] is not int statusCode) return;

        executed.Result = new ObjectResult(new
        {
            code = exception.Data["Code"] as string,
            message = exception.Message,
            detail = exception.Message,
            proposedHandling = exception.Data["ProposedHandling"] as string,
            isDraftPolicy = exception.Data["IsDraftPolicy"] is true
        }) { StatusCode = statusCode };
        executed.ExceptionHandled = true;
    }
}

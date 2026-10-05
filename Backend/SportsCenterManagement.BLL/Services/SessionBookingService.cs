using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// UC-43 / UC-44. BR-MEM-01 (active package), BR-BOOK-01 (30 min – 7 days window),
/// BR-BOOK-02 (cancel ≥ 2h before), BR-BOOK-03 (no overlapping sessions the same day),
/// BR-BOOK-04 (promote waitlist), BR-CLASS-01 (capacity).
/// </summary>
public sealed class SessionBookingService(IUnitOfWork unitOfWork) : ISessionBookingService
{
    private static readonly TimeSpan MinLeadTime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxLeadTime = TimeSpan.FromDays(7);
    private static readonly TimeSpan CancelCutoff = TimeSpan.FromHours(2);

    private SportsCenterDbContext Db => unitOfWork.Context;

    // ------------------------------------------------------------------ UC-43

    public async Task<BookSessionResult> BookSessionAsync(long currentUserId, long sessionId, CancellationToken cancellationToken = default)
    {
        var member = await GetMemberAsync(currentUserId, cancellationToken);

        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var session = await Db.ClassSessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw FlowException.NotFound("Buổi học không tồn tại."); // UH06
        var cls = await Db.Classes.SingleAsync(c => c.Id == session.ClassId, cancellationToken);
        var now = ScheduleTime.VietnamNow;

        await EnsureEligibleAsync(member, session, cls, now, cancellationToken);

        var capacity = await GetEffectiveCapacityAsync(session, cls, cancellationToken);
        var booked = await Db.SessionBookings.CountAsync(
            b => b.SessionId == sessionId && b.Status == FlowStatuses.BookingBooked, cancellationToken);

        if (booked >= capacity)
        {
            // UH03 / UH07: full (possibly just now) -> waitlist instead of a hard failure.
            var entry = await Db.ClassWaitlists.FirstOrDefaultAsync(
                w => w.ClassId == cls.Id && w.MemberId == member.Id && w.Status == FlowStatuses.WaitlistWaiting, cancellationToken);
            if (entry is null)
            {
                entry = new ClassWaitlist
                {
                    ClassId = cls.Id,
                    MemberId = member.Id,
                    JoinedAt = DateTime.UtcNow,
                    Status = FlowStatuses.WaitlistWaiting
                };
                await Db.ClassWaitlists.AddAsync(entry, cancellationToken);
                await SaveAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new BookSessionResult(false, true, null, entry.Id,
                "Lớp vừa đầy, bạn đã được chuyển vào danh sách chờ.");
        }

        var booking = await CreateOrReactivateBookingAsync(member.Id, sessionId, cancellationToken);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BookSessionResult(true, false, ToResponse(booking, session, cls), null, "Đặt lịch thành công.");
    }

    // ------------------------------------------------------------------ UC-44

    public async Task<CancelBookingResult> CancelBookingAsync(long currentUserId, long bookingId, CancellationToken cancellationToken = default)
    {
        var member = await GetMemberAsync(currentUserId, cancellationToken);

        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var booking = await Db.SessionBookings.SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw FlowException.NotFound("Không tìm thấy lượt đặt chỗ.");
        if (booking.MemberId != member.Id) // UH02
            throw FlowException.Forbidden("Bạn không thể hủy lượt đặt của người khác.");
        if (booking.Status != FlowStatuses.BookingBooked) // UH03
            throw FlowException.Conflict("Lượt đặt chỗ này đã được hủy trước đó.");

        var session = await Db.ClassSessions.SingleAsync(s => s.Id == booking.SessionId, cancellationToken);
        var cls = await Db.Classes.SingleAsync(c => c.Id == session.ClassId, cancellationToken);
        var now = ScheduleTime.VietnamNow;
        var start = session.SessionDate.ToDateTime(session.StartTime);

        if (start <= now) // UH04
            throw FlowException.BadRequest("Không thể hủy buổi đã qua.");

        var late = start - now < CancelCutoff; // BR-BOOK-02
        booking.Status = late ? FlowStatuses.BookingCancelledLate : FlowStatuses.BookingCancelled;

        long? promotedMemberId = null;
        if (!late)
        {
            await SaveAsync(cancellationToken); // free the seat before promoting
            promotedMemberId = await PromoteNextWaitlistAsync(session, cls, now, cancellationToken);
        }

        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var message = late
            ? "Hủy muộn (dưới 2 giờ trước giờ học): buổi này vẫn được tính là đã tham gia."
            : "Hủy đặt chỗ thành công.";
        return new CancelBookingResult(ToResponse(booking, session, cls), late, promotedMemberId, message);
    }

    public async Task<IReadOnlyList<SessionBookingResponse>> ListMyBookingsAsync(long currentUserId, CancellationToken cancellationToken = default)
    {
        var member = await GetMemberAsync(currentUserId, cancellationToken);
        var rows = await (
            from b in Db.SessionBookings
            join s in Db.ClassSessions on b.SessionId equals s.Id
            join c in Db.Classes on s.ClassId equals c.Id
            where b.MemberId == member.Id
            orderby s.SessionDate descending, s.StartTime descending
            select new { b, s, c }).ToListAsync(cancellationToken);
        return rows.Select(r => ToResponse(r.b, r.s, r.c)).ToList();
    }

    // ------------------------------------------------------------------ rules

    /// <summary>Throws when the member may not take a seat in the session right now.</summary>
    private async Task EnsureEligibleAsync(MemberProfile member, ClassSession session, ClassEntity cls, DateTime now, CancellationToken ct)
    {
        if (member.BookingSuspendedUntil is { } until && until > DateTime.UtcNow)
            throw FlowException.Forbidden($"Bạn đang bị tạm khóa đặt lịch đến {until:dd/MM/yyyy HH:mm}.");

        if (session.SessionStatus != FlowStatuses.SessionScheduled) // UH06
            throw FlowException.Conflict("Buổi học đã bị hủy hoặc không còn nhận đặt chỗ.");
        if (FlowStatuses.IsClosed(cls.Status))
            throw FlowException.Conflict("Lớp học đã bị hủy hoặc đã kết thúc.");

        // BR-MEM-01
        var hasPackage = await Db.MemberSubscriptions.AnyAsync(s =>
            s.MemberId == member.Id && s.Status == "Active"
            && s.StartDate <= session.SessionDate && s.EndDate >= session.SessionDate, ct);
        if (!hasPackage)
            throw FlowException.Forbidden("Bạn cần có gói tập còn hiệu lực để đặt lịch.");

        // BR-BOOK-01
        var start = session.SessionDate.ToDateTime(session.StartTime);
        var lead = start - now;
        if (lead < MinLeadTime || lead > MaxLeadTime)
            throw FlowException.BadRequest("Chỉ được đặt lịch trước giờ học tối thiểu 30 phút và tối đa 7 ngày.");

        // UH05
        var alreadyBooked = await Db.SessionBookings.AnyAsync(
            b => b.SessionId == session.Id && b.MemberId == member.Id && b.Status == FlowStatuses.BookingBooked, ct);
        if (alreadyBooked)
            throw FlowException.Conflict("Bạn đã đặt buổi học này rồi.");

        // BR-BOOK-03
        var sameDay = await (
            from b in Db.SessionBookings
            join s in Db.ClassSessions on b.SessionId equals s.Id
            join c in Db.Classes on s.ClassId equals c.Id
            where b.MemberId == member.Id && b.Status == FlowStatuses.BookingBooked
                && s.SessionStatus == FlowStatuses.SessionScheduled
                && s.SessionDate == session.SessionDate && s.Id != session.Id
            select new { s.StartTime, s.EndTime, ClassName = c.Name }).ToListAsync(ct);
        var clash = sameDay.FirstOrDefault(x => ScheduleTime.Overlaps(session.StartTime, session.EndTime, x.StartTime, x.EndTime));
        if (clash is not null)
            throw FlowException.Conflict(
                $"Bạn đã có buổi '{clash.ClassName}' ({clash.StartTime:HH\\:mm}-{clash.EndTime:HH\\:mm}) trùng giờ trong ngày này.");
    }

    private async Task<int> GetEffectiveCapacityAsync(ClassSession session, ClassEntity cls, CancellationToken ct)
    {
        var roomId = session.RoomId ?? cls.RoomId;
        if (roomId is null) return cls.Capacity;
        var roomCapacity = await Db.Rooms.Where(r => r.Id == roomId).Select(r => (int?)r.Capacity).FirstOrDefaultAsync(ct);
        return roomCapacity is { } rc ? Math.Min(cls.Capacity, rc) : cls.Capacity;
    }

    /// <summary>(session_id, member_id) is unique, so a previously cancelled row is re-used instead of duplicated.</summary>
    private async Task<SessionBooking> CreateOrReactivateBookingAsync(long memberId, long sessionId, CancellationToken ct)
    {
        var existing = await Db.SessionBookings.SingleOrDefaultAsync(b => b.SessionId == sessionId && b.MemberId == memberId, ct);
        if (existing is not null)
        {
            existing.Status = FlowStatuses.BookingBooked;
            existing.BookedAt = DateTime.UtcNow;
            return existing;
        }

        var booking = new SessionBooking
        {
            SessionId = sessionId,
            MemberId = memberId,
            BookedAt = DateTime.UtcNow,
            Status = FlowStatuses.BookingBooked
        };
        await Db.SessionBookings.AddAsync(booking, ct);
        return booking;
    }

    /// <summary>
    /// BR-BOOK-04. Promotes the earliest waiting member of the class who is still eligible for the freed session.
    /// Ineligible members (no active package, clash, ...) stay in the waitlist.
    /// </summary>
    private async Task<long?> PromoteNextWaitlistAsync(ClassSession session, ClassEntity cls, DateTime now, CancellationToken ct)
    {
        var capacity = await GetEffectiveCapacityAsync(session, cls, ct);
        var booked = await Db.SessionBookings.CountAsync(
            b => b.SessionId == session.Id && b.Status == FlowStatuses.BookingBooked, ct);
        if (booked >= capacity) return null;

        var waiting = await Db.ClassWaitlists
            .Where(w => w.ClassId == cls.Id && w.Status == FlowStatuses.WaitlistWaiting)
            .OrderBy(w => w.JoinedAt).ThenBy(w => w.Id)
            .ToListAsync(ct);

        foreach (var entry in waiting)
        {
            var candidate = await Db.MemberProfiles.SingleOrDefaultAsync(m => m.Id == entry.MemberId, ct);
            if (candidate is null) continue;
            try
            {
                await EnsureEligibleAsync(candidate, session, cls, now, ct);
            }
            catch (FlowException)
            {
                continue;
            }

            await CreateOrReactivateBookingAsync(candidate.Id, session.Id, ct);
            entry.Status = FlowStatuses.WaitlistPromoted;
            return candidate.Id;
        }

        return null; // empty or no eligible waitlist: seat is simply released (not an error)
    }

    // ------------------------------------------------------------------ helpers

    private async Task<MemberProfile> GetMemberAsync(long userId, CancellationToken ct) =>
        await Db.MemberProfiles.SingleOrDefaultAsync(m => m.UserId == userId, ct)
        ?? throw FlowException.Forbidden("Tài khoản hiện tại chưa có hồ sơ hội viên.");

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique (session_id, member_id) or serialization conflict under concurrent requests.
            throw FlowException.Conflict("Yêu cầu bị xung đột với một thao tác khác, vui lòng thử lại.");
        }
    }

    private static SessionBookingResponse ToResponse(SessionBooking b, ClassSession s, ClassEntity c) => new(
        b.Id, b.SessionId, c.Id, c.Name, s.SessionDate, s.StartTime, s.EndTime, b.Status, b.BookedAt);
}

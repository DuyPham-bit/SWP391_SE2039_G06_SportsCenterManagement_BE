using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Classes;
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
public sealed class SessionBookingService(IUnitOfWork unitOfWork, SportsCenterManagement.DAL.Repositories.Interfaces.IClassRepository classRepository, ICenterScopeService centerScopeService) : ISessionBookingService
{
    private static readonly TimeSpan MinLeadTime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxLeadTime = TimeSpan.FromDays(7);
    private static readonly TimeSpan CancelCutoff = TimeSpan.FromHours(2);

    private SportsCenterDbContext Db => unitOfWork.Context;

    // ------------------------------------------------------------------ UC-43

    public async Task<BookSessionResult> BookSessionAsync(long currentUserId, long sessionId, CancellationToken cancellationToken = default, long? actorUserId = null)
    {
        var member = await GetMemberAsync(currentUserId, cancellationToken);

        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var session = await Db.ClassSessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Buổi học không tồn tại.") { Data = { ["StatusCode"] = 404 } }; // UH06
        var cls = await Db.Classes.SingleAsync(c => c.Id == session.ClassId, cancellationToken);
        var now = ScheduleTime.VietnamNow;

        // A seat may have opened without a cancellation (capacity raised, room changed). Members
        // already queued keep their FIFO priority, so free seats go to them before a new booker.
        if (cls.AllowWaitlist && await Db.ClassWaitlists.AnyAsync(
                w => w.SessionId == sessionId && w.Status == FlowStatuses.WaitlistWaiting, cancellationToken))
            await FillOpenSeatsFromWaitlistAsync(sessionId, actorUserId ?? currentUserId, cancellationToken);

        // A retry after a successful response must return the existing active booking,
        // even if the member's package state changed after the original request.
        var existingBooking = await Db.SessionBookings.SingleOrDefaultAsync(
            booking => booking.SessionId == sessionId && booking.MemberId == member.Id
                && booking.Status == FlowStatuses.BookingBooked,
            cancellationToken);
        if (existingBooking is not null)
        {
            await CancelWaitingEntriesAsync(sessionId, member.Id, cancellationToken);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new BookSessionResult(true, false, ToResponse(existingBooking, session, cls), null,
                "Bạn đã đặt buổi học này trước đó.");
        }

        var subscription = await EnsureEligibleAsync(member, session, cls, now, cancellationToken);

        var capacity = await GetEffectiveCapacityAsync(session, cls, cancellationToken);
        var booked = await Db.SessionBookings.CountAsync(
            b => b.SessionId == sessionId && b.Status == FlowStatuses.BookingBooked, cancellationToken);

        if (booked >= capacity)
        {
            if (!cls.AllowWaitlist)
                throw new InvalidOperationException("Buổi học đã đủ chỗ và không nhận danh sách chờ.") { Data = { ["StatusCode"] = 409 } };

            // Waitlist belongs to a concrete session; a queue for another date must not consume this seat.
            var entry = await Db.ClassWaitlists.FirstOrDefaultAsync(
                w => w.SessionId == sessionId && w.MemberId == member.Id && w.Status == FlowStatuses.WaitlistWaiting, cancellationToken);
            if (entry is null)
            {
                entry = new ClassWaitlist
                {
                    SessionId = sessionId,
                    ClassId = cls.Id,
                    MemberId = member.Id,
                    SubscriptionId = subscription.Id,
                    JoinedAt = DateTime.UtcNow,
                    Status = FlowStatuses.WaitlistWaiting
                };
                await Db.ClassWaitlists.AddAsync(entry, cancellationToken);
                AuditLogWriter.Add(unitOfWork, actorUserId ?? currentUserId, cls.CenterId, "JOIN_SESSION_WAITLIST", "ClassSession", sessionId);
                await NotifyMemberAsync(actorUserId ?? currentUserId, member.Id, session, cls,
                    "Đã tham gia danh sách chờ", "Bạn đã vào danh sách chờ của buổi học", "WaitlistJoined", cancellationToken);
                await SaveAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new BookSessionResult(false, true, null, entry.Id,
                "Lớp vừa đầy, bạn đã được chuyển vào danh sách chờ.");
        }

        var booking = await CreateOrReactivateBookingAsync(member.Id, sessionId, subscription.Id, cancellationToken);
        await CancelWaitingEntriesAsync(sessionId, member.Id, cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId ?? currentUserId, cls.CenterId, "BOOK_SESSION", "ClassSession", sessionId);
        await NotifyMemberAsync(actorUserId ?? currentUserId, member.Id, session, cls,
            "Đặt lớp thành công", "Bạn đã được xác nhận chỗ học", "BookingConfirmed", cancellationToken);
        await NotifyCoachesAsync(actorUserId ?? currentUserId, member.FullName, session, cls,
            "Học viên đặt lịch", "đã đặt buổi học", "CoachBookingCreated", cancellationToken);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BookSessionResult(true, false, ToResponse(booking, session, cls), null, "Đặt lịch thành công.");
    }

    // ------------------------------------------------------------------ UC-44

    public async Task<CancelBookingResult> CancelBookingAsync(long currentUserId, long bookingId, CancellationToken cancellationToken = default, long? actorUserId = null)
    {
        var member = await GetMemberAsync(currentUserId, cancellationToken);

        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var booking = await Db.SessionBookings.SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy lượt đặt chỗ.") { Data = { ["StatusCode"] = 404 } };
        if (booking.MemberId != member.Id) // UH02
            throw new InvalidOperationException("Bạn không thể hủy lượt đặt của người khác.") { Data = { ["StatusCode"] = 403 } };
        if (booking.Status != FlowStatuses.BookingBooked) // UH03
            throw new InvalidOperationException("Lượt đặt chỗ này đã được hủy trước đó.") { Data = { ["StatusCode"] = 409 } };

        var session = await Db.ClassSessions.SingleAsync(s => s.Id == booking.SessionId, cancellationToken);
        var cls = await Db.Classes.SingleAsync(c => c.Id == session.ClassId, cancellationToken);
        var now = ScheduleTime.VietnamNow;
        var start = session.SessionDate.ToDateTime(session.StartTime);

        if (start <= now) // UH04
            throw new InvalidOperationException("Không thể hủy buổi đã qua.") { Data = { ["StatusCode"] = 400 } };

        // Package rule: block cancellation inside the package window; 0 keeps the legacy late-cancel penalty.
        var cancelBeforeHours = booking.SubscriptionId is { } subscriptionId
            ? await (from subscription in Db.MemberSubscriptions
                     join package in Db.MembershipPackages on subscription.PackageId equals package.Id
                     where subscription.Id == subscriptionId
                     select package.CancelBeforeHours).FirstOrDefaultAsync(cancellationToken)
            : 0;
        if (cancelBeforeHours > 0 && start - now < TimeSpan.FromHours(cancelBeforeHours))
            throw new InvalidOperationException($"Gói của bạn yêu cầu hủy lịch trước ít nhất {cancelBeforeHours} giờ.")
            {
                Data = { ["StatusCode"] = 409, ["Code"] = "BOOK-CANCEL-WINDOW" }
            };

        var late = start - now < CancelCutoff; // BR-BOOK-02
        booking.Status = late ? FlowStatuses.BookingCancelledLate : FlowStatuses.BookingCancelled;
        AuditLogWriter.Add(unitOfWork, actorUserId ?? currentUserId, cls.CenterId, "CANCEL_SESSION_BOOKING", "SessionBooking", bookingId, newValues: new { IsLateCancellation = late });

        // A late cancellation still carries the member penalty, but the physical seat
        // is free and must be offered fairly to the waitlist before direct booking.
        await SaveAsync(cancellationToken);
        var promotedMemberId = await PromoteNextWaitlistAsync(
            session, cls, now, actorUserId ?? currentUserId, cancellationToken);

        await NotifyMemberAsync(actorUserId ?? currentUserId, member.Id, session, cls,
            "Đã hủy đặt lớp", late ? "Bạn đã hủy muộn; lượt tập vẫn bị tính nhưng bạn không được ghi nhận có mặt" : "Chỗ học của bạn đã được hủy",
            "BookingCancelled", cancellationToken);
        await NotifyCoachesAsync(actorUserId ?? currentUserId, member.FullName, session, cls,
            "Học viên hủy lịch", late ? "đã hủy muộn buổi học" : "đã hủy buổi học",
            "CoachBookingCancelled", cancellationToken);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var message = late
            ? "Hủy muộn (dưới 2 giờ trước giờ học): lượt tập vẫn bị tính nhưng không được ghi nhận có mặt."
            : "Hủy đặt chỗ thành công.";
        if (promotedMemberId.HasValue)
            message += " Chỗ trống đã được tự động chuyển cho hội viên đầu tiên đủ điều kiện trong danh sách chờ.";
        return new CancelBookingResult(ToResponse(booking, session, cls), late, promotedMemberId, message);
    }

    public async Task<IReadOnlyList<SessionBookingResponse>> ListMyBookingsAsync(long currentUserId, CancellationToken cancellationToken = default)
    {
        var member = await GetMemberAsync(currentUserId, cancellationToken);
        return await (
            from b in Db.SessionBookings
            join s in Db.ClassSessions on b.SessionId equals s.Id
            join c in Db.Classes on s.ClassId equals c.Id
            join room in Db.Rooms on s.RoomId equals (long?)room.Id into rooms
            from room in rooms.DefaultIfEmpty()
            join coach in Db.CoachProfiles on s.CoachId equals (long?)coach.Id into coaches
            from coach in coaches.DefaultIfEmpty()
            join attendance in Db.Attendances on new { b.SessionId, b.MemberId }
                equals new { attendance.SessionId, attendance.MemberId } into attendances
            from attendance in attendances.DefaultIfEmpty()
            where b.MemberId == member.Id
            orderby s.SessionDate descending, s.StartTime descending
            select new SessionBookingResponse(
                b.Id, b.SessionId, c.Id, c.Name, s.SessionDate, s.StartTime, s.EndTime, b.Status, b.BookedAt,
                s.SessionStatus, room == null ? null : room.Name,
                coach == null ? null : coach.FullName, coach == null ? null : coach.Specialization,
                attendance == null ? null : attendance.AttendanceStatus,
                attendance == null ? null : attendance.Note,
                attendance == null ? null : attendance.CheckInTime)).ToListAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ rules

    /// <summary>Throws when the member may not take a seat in the session right now.</summary>
    private async Task<MemberSubscription> EnsureEligibleAsync(
        MemberProfile member, ClassSession session, ClassEntity cls, DateTime now, CancellationToken ct,
        bool enforceMinimumLeadTime = true)
    {
        var activeUser = await Db.Users.AnyAsync(user => user.Id == member.UserId && user.Status == "Active"
            && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow), ct);
        if (!activeUser)
            throw new InvalidOperationException("Tài khoản hội viên không hoạt động.") { Data = { ["StatusCode"] = 403 } };
        if (!BookingAccessRules.IsMemberAssignedToClassCenter(member.CenterId, cls.CenterId))
            throw new InvalidOperationException("Lớp học không thuộc trung tâm của hội viên.") { Data = { ["StatusCode"] = 403 } };

        if (member.BookingSuspendedUntil is { } until && until > DateTime.UtcNow)
            throw new InvalidOperationException($"Bạn đang bị tạm khóa đặt lịch đến {until:dd/MM/yyyy HH:mm}.") { Data = { ["StatusCode"] = 403 } };

        if (session.SessionStatus != FlowStatuses.SessionScheduled) // UH06
            throw new InvalidOperationException("Buổi học đã bị hủy hoặc không còn nhận đặt chỗ.") { Data = { ["StatusCode"] = 409 } };
        if (cls.Status != FlowStatuses.ClassPublished)
            throw new InvalidOperationException("Lớp học chưa được mở đăng ký hoặc đã đóng.") { Data = { ["StatusCode"] = 409 } };
        if (!await Db.Centers.AnyAsync(center => center.Id == cls.CenterId && center.Status == "Active", ct))
            throw new InvalidOperationException("Trung tâm của lớp hiện không nhận đặt lịch.") { Data = { ["StatusCode"] = 409 } };
        if (!await Db.Sports.AnyAsync(sport => sport.Id == cls.SportId && sport.Status == "Active"
            && (!sport.CenterId.HasValue || sport.CenterId.Value == cls.CenterId), ct))
            throw new InvalidOperationException("Bộ môn của lớp hiện không hoạt động tại trung tâm.") { Data = { ["StatusCode"] = 409 } };

        var roomId = session.RoomId ?? cls.RoomId;
        if (roomId.HasValue && !await Db.Rooms.AnyAsync(room => room.Id == roomId.Value
                && room.CenterId == cls.CenterId && room.Status == "Active", ct))
            throw new InvalidOperationException("Phòng học đã ngừng hoạt động hoặc không còn thuộc trung tâm của lớp.") { Data = { ["StatusCode"] = 409 } };

        var hasActivePrimaryCoach = await (
            from assignment in Db.ClassCoaches
            join coach in Db.CoachProfiles on assignment.CoachId equals coach.Id
            join user in Db.Users on coach.UserId equals user.Id
            where assignment.ClassId == cls.Id && assignment.IsPrimary
                && coach.CenterId == cls.CenterId && coach.Status == "Active" && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)
            select assignment.CoachId).AnyAsync(ct);
        if (!hasActivePrimaryCoach)
            throw new InvalidOperationException("Lớp chưa có huấn luyện viên chính đang hoạt động; hãy liên hệ trung tâm.") { Data = { ["StatusCode"] = 409 } };

        // BR-MEM-01
        var eligibleSubscriptions = await (
            from subscription in Db.MemberSubscriptions
            join package in Db.MembershipPackages on subscription.PackageId equals package.Id
            where subscription.MemberId == member.Id && subscription.Status == "Active"
                && subscription.StartDate <= session.SessionDate && subscription.EndDate >= session.SessionDate
                && package.CenterId == cls.CenterId
            orderby package.AllowedSports descending, subscription.EndDate descending
            select new { Subscription = subscription, package.MaxClasses, package.AllowedSports, package.IsAllAccess }).ToListAsync(ct);
        if (eligibleSubscriptions.Count == 0)
            throw new InvalidOperationException("Bạn cần có gói tập còn hiệu lực tại trung tâm để đặt lịch buổi học này.")
            {
                Data =
                {
                    ["StatusCode"] = 403,
                    ["Code"] = "ME-05",
                    ["ProposedHandling"] = "Kiểm tra quyền sử dụng tại thời điểm buổi học; thông báo điều kiện chưa đáp ứng và hướng dẫn chọn gói/lớp phù hợp."
                }
            };

        var enrollmentEntitlements = await (
            from enrollment in Db.ClassEnrollments
            join enrolledClass in Db.Classes on enrollment.ClassId equals enrolledClass.Id
            where enrollment.MemberId == member.Id && enrollment.Status == FlowStatuses.EnrollmentConfirmed
            select new { enrollment.ClassId, enrolledClass.SportId, enrollment.SubscriptionId }).ToListAsync(ct);
        var bookedEntitlements = await (
            from booking in Db.SessionBookings
            join bookedSession in Db.ClassSessions on booking.SessionId equals bookedSession.Id
            join bookedClass in Db.Classes on bookedSession.ClassId equals bookedClass.Id
            where booking.MemberId == member.Id
                && (booking.Status == FlowStatuses.BookingBooked
                    || booking.Status == FlowStatuses.BookingCancelledLate)
            select new { ClassId = bookedSession.ClassId, bookedClass.SportId, booking.SubscriptionId }).ToListAsync(ct);

        var candidateIds = eligibleSubscriptions.Select(row => row.Subscription.Id).ToList();
        var selectedSports = await Db.SubscriptionSports.Where(row => candidateIds.Contains(row.SubscriptionId))
            .Select(row => new { row.SubscriptionId, row.SportId }).ToListAsync(ct);

        MemberSubscription? selectedSubscription = null;
        var sportExcluded = true;
        foreach (var candidate in eligibleSubscriptions)
        {
            var picked = selectedSports.Where(row => row.SubscriptionId == candidate.Subscription.Id)
                .Select(row => row.SportId).ToHashSet();
            var entitlements = enrollmentEntitlements
                .Where(row => row.SubscriptionId == candidate.Subscription.Id)
                .Concat(bookedEntitlements.Where(row => row.SubscriptionId == candidate.Subscription.Id))
                .DistinctBy(row => row.ClassId)
                .ToArray();
            var enrolledClassIds = entitlements.Select(row => row.ClassId).ToHashSet();
            var enrolledSportIds = entitlements.Select(row => row.SportId).ToHashSet();
            var classLimitAvailable = candidate.MaxClasses is null || enrolledClassIds.Contains(cls.Id)
                || enrolledClassIds.Count < candidate.MaxClasses.Value;
            // Explicit selection (bought after sport picking existed) wins; older subscriptions keep the implicit rule.
            var sportLimitAvailable = candidate.IsAllAccess
                || (picked.Count > 0
                    ? picked.Contains(cls.SportId)
                    : enrolledSportIds.Contains(cls.SportId) || enrolledSportIds.Count < candidate.AllowedSports);
            if (sportLimitAvailable) sportExcluded = false;
            if (classLimitAvailable && sportLimitAvailable)
            {
                selectedSubscription = candidate.Subscription;
                break;
            }
        }
        if (selectedSubscription is null)
            throw new InvalidOperationException(sportExcluded
                ? "Bộ môn này không nằm trong gói tập của bạn."
                : "Gói tập đã sử dụng hết số lớp được phép.")
            {
                Data =
                {
                    ["StatusCode"] = 403,
                    ["Code"] = "ME-05",
                    ["ProposedHandling"] = "Kiểm tra quyền sử dụng tại thời điểm buổi học; thông báo điều kiện chưa đáp ứng và hướng dẫn chọn gói/lớp phù hợp."
                }
            };

        // BR-BOOK-01
        var start = session.SessionDate.ToDateTime(session.StartTime);
        var lead = start - now;
        if (lead <= TimeSpan.Zero || lead > MaxLeadTime || (enforceMinimumLeadTime && lead < MinLeadTime))
            throw new InvalidOperationException("Chỉ được đặt lịch trước giờ học tối thiểu 30 phút và tối đa 7 ngày.")
            {
                Data =
                {
                    ["StatusCode"] = 400,
                    ["Code"] = "ME-04",
                    ["ProposedHandling"] = "Kiểm tra tại thời điểm xác nhận; từ chối với lý do cụ thể."
                }
            };

        // UH05
        var alreadyBooked = await Db.SessionBookings.AnyAsync(
            b => b.SessionId == session.Id && b.MemberId == member.Id && b.Status == FlowStatuses.BookingBooked, ct);
        if (alreadyBooked)
            throw new InvalidOperationException("Bạn đã đặt buổi học này rồi.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "ME-04",
                    ["ProposedHandling"] = "Kiểm tra tại thời điểm xác nhận; từ chối với lý do cụ thể."
                }
            };

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
            throw new InvalidOperationException($"Bạn đã có buổi '{clash.ClassName}' ({clash.StartTime:HH\\:mm}-{clash.EndTime:HH\\:mm}) trùng giờ trong ngày này.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "ME-04",
                    ["ProposedHandling"] = "Kiểm tra tại thời điểm xác nhận; từ chối với lý do cụ thể. Hai người tranh suất cuối chỉ một người được xác nhận."
                }
            };

        return selectedSubscription;
    }

    private async Task<int> GetEffectiveCapacityAsync(ClassSession session, ClassEntity cls, CancellationToken ct)
    {
        var roomId = session.RoomId ?? cls.RoomId;
        if (roomId is null) return cls.Capacity;
        var roomCapacity = await Db.Rooms.Where(r => r.Id == roomId).Select(r => (int?)r.Capacity).FirstOrDefaultAsync(ct);
        return roomCapacity is { } rc ? Math.Min(cls.Capacity, rc) : cls.Capacity;
    }

    /// <summary>(session_id, member_id) is unique, so a previously cancelled row is re-used instead of duplicated.</summary>
    private async Task<SessionBooking> CreateOrReactivateBookingAsync(
        long memberId, long sessionId, long subscriptionId, CancellationToken ct)
    {
        var existing = await Db.SessionBookings.SingleOrDefaultAsync(b => b.SessionId == sessionId && b.MemberId == memberId, ct);
        if (existing is not null)
        {
            existing.Status = FlowStatuses.BookingBooked;
            existing.BookedAt = DateTime.UtcNow;
            existing.SubscriptionId = subscriptionId;
            return existing;
        }

        var booking = new SessionBooking
        {
            SessionId = sessionId,
            MemberId = memberId,
            SubscriptionId = subscriptionId,
            BookedAt = DateTime.UtcNow,
            Status = FlowStatuses.BookingBooked
        };
        await Db.SessionBookings.AddAsync(booking, ct);
        return booking;
    }

    /// <summary>
    /// BR-BOOK-04. Promotes the earliest waiting member of the class who is still eligible for the freed session.
    /// Ineligible members (no active package, clash, ...) are closed and the next FIFO entry is evaluated.
    /// </summary>
    private async Task<long?> PromoteNextWaitlistAsync(ClassSession session, ClassEntity cls, DateTime now, long actorUserId, CancellationToken ct)
    {
        var capacity = await GetEffectiveCapacityAsync(session, cls, ct);
        var booked = await Db.SessionBookings.CountAsync(
            b => b.SessionId == session.Id && b.Status == FlowStatuses.BookingBooked, ct);
        if (booked >= capacity) return null;

        var waiting = await Db.ClassWaitlists
            .Where(w => w.SessionId == session.Id && w.ClassId == cls.Id && w.Status == FlowStatuses.WaitlistWaiting)
            .OrderBy(w => w.JoinedAt).ThenBy(w => w.Id)
            .ToListAsync(ct);

        foreach (var entry in waiting)
        {
            if (await Db.SessionBookings.AnyAsync(
                    booking => booking.SessionId == session.Id && booking.MemberId == entry.MemberId
                        && booking.Status == FlowStatuses.BookingBooked, ct))
            {
                entry.Status = FlowStatuses.WaitlistCancelled;
                continue;
            }

            var candidate = await Db.MemberProfiles.SingleOrDefaultAsync(m => m.Id == entry.MemberId, ct);
            if (candidate is null) continue;
            try
            {
                // The member explicitly opted into auto-promotion when joining the waitlist.
                // Direct bookings keep the 30-minute lead-time rule; a waiting member may
                // receive a newly freed seat until the session actually starts.
                var subscription = await EnsureEligibleAsync(
                    candidate, session, cls, now, ct, enforceMinimumLeadTime: false);
                await CreateOrReactivateBookingAsync(candidate.Id, session.Id, subscription.Id, ct);
            }
            catch (InvalidOperationException failure) when (failure.Data["StatusCode"] is int)
            {
                entry.Status = FlowStatuses.WaitlistCancelled;
                continue;
            }

            entry.Status = FlowStatuses.WaitlistPromoted;
            await NotifyMemberAsync(actorUserId, candidate.Id, session, cls, "Đã có chỗ học từ danh sách chờ",
                "Bạn đã được tự động xác nhận chỗ học từ danh sách chờ", "WaitlistPromoted", ct);
            await NotifyCoachesAsync(actorUserId, candidate.FullName, session, cls,
                "Học viên được nhận từ danh sách chờ", "đã được tự động xác nhận chỗ học",
                "CoachWaitlistPromoted", ct);
            return candidate.Id;
        }

        return null; // empty or no eligible waitlist: seat is simply released (not an error)
    }

    // ------------------------------------------------------------------ helpers

    private Task NotifyMemberAsync(long actorUserId, long memberId, ClassSession session, ClassEntity cls,
        string title, string message, string type, CancellationToken ct) =>
        FlowNotificationWriter.CreateForMemberAsync(Db, actorUserId, memberId, title,
            $"{message}: {cls.Name}, ngày {session.SessionDate:dd/MM/yyyy} lúc {session.StartTime:HH:mm}.", type, ct);

    private async Task NotifyCoachesAsync(long actorUserId, string memberName, ClassSession session, ClassEntity cls,
        string title, string action, string type, CancellationToken ct)
    {
        var coachUserIds = await Db.CoachProfiles
            .Where(coach => coach.Status == "Active" &&
                (coach.Id == session.CoachId || Db.ClassCoaches.Any(link =>
                    link.ClassId == session.ClassId && link.CoachId == coach.Id)))
            .Select(coach => coach.UserId).Distinct().ToArrayAsync(ct);
        if (coachUserIds.Length == 0) return;

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            SenderId = actorUserId,
            Title = title,
            Message = $"{memberName} {action}: {cls.Name}, ngày {session.SessionDate:dd/MM/yyyy} lúc {session.StartTime:HH:mm}.",
            NotificationType = type,
            CreatedAt = now
        };
        await Db.Notifications.AddAsync(notification, ct);
        await SaveAsync(ct);

        Db.UserNotifications.AddRange(coachUserIds.Select(userId => new UserNotification
        {
            NotificationId = notification.Id,
            UserId = userId,
            IsRead = false,
            CreatedAt = now
        }));
        var channels = new[] { "InApp", "Email" };
        Db.CommunicationDeliveries.AddRange(coachUserIds.SelectMany(userId => channels.Select(channel =>
            new CommunicationDelivery
            {
                NotificationId = notification.Id,
                RecipientUserId = userId,
                Channel = channel,
                Status = channel == "InApp" ? "Delivered" : "Queued",
                ProviderMessage = channel == "InApp"
                    ? "Đã giao trong ứng dụng"
                    : "Đang chờ cấu hình cổng gửi bên ngoài",
                CreatedAt = now,
                SentAt = channel == "InApp" ? now : null
            })));
        await SaveAsync(ct);
    }

    private async Task<MemberProfile> GetMemberAsync(long userId, CancellationToken ct) =>
        await Db.MemberProfiles.SingleOrDefaultAsync(m => m.UserId == userId, ct)
        ?? throw new InvalidOperationException("Tài khoản hiện tại chưa có hồ sơ hội viên.") { Data = { ["StatusCode"] = 403 } };

    private async Task CancelWaitingEntriesAsync(long sessionId, long memberId, CancellationToken ct)
    {
        var entries = await Db.ClassWaitlists
            .Where(waitlist => waitlist.SessionId == sessionId && waitlist.MemberId == memberId
                && waitlist.Status == FlowStatuses.WaitlistWaiting)
            .ToListAsync(ct);
        foreach (var entry in entries)
            entry.Status = FlowStatuses.WaitlistCancelled;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique (session_id, member_id) or serialization conflict under concurrent requests.
            throw new InvalidOperationException("Yêu cầu bị xung đột với một thao tác khác, vui lòng thử lại.") { Data = { ["StatusCode"] = 409 } };
        }
    }

    private static SessionBookingResponse ToResponse(SessionBooking b, ClassSession s, ClassEntity c) => new(
        b.Id, b.SessionId, c.Id, c.Name, s.SessionDate, s.StartTime, s.EndTime, b.Status, b.BookedAt);

    public async Task<long> ResolveMemberUserIdAsync(long actorUserId, bool isAdmin, long memberId, CancellationToken ct = default)
    {
        var member = await classRepository.GetMemberUserAndCenterAsync(memberId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy hội viên.") { Data = { ["StatusCode"] = 404 } };
        if (member.CenterId is not { } centerId)
            throw new InvalidOperationException("Hội viên chưa được gán trung tâm.") { Data = { ["StatusCode"] = 403 } };
        await centerScopeService.EnsureCenterAccessAsync(actorUserId, isAdmin, centerId, ct);
        return member.UserId;
    }

    public async Task<BookSessionResult> BookForMemberAsync(long actorUserId, bool isAdmin, long memberId, long sessionId, CancellationToken ct = default)
    {
        var userId = await ResolveMemberUserIdAsync(actorUserId, isAdmin, memberId, ct);
        var classId = await classRepository.GetClassIdBySessionIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy buổi học.") { Data = { ["StatusCode"] = 404 } };
        await centerScopeService.EnsureClassAccessAsync(actorUserId, isAdmin, classId, ct);
        return await BookSessionAsync(userId, sessionId, ct, actorUserId);
    }

    public async Task<CancelBookingResult> CancelForMemberAsync(long actorUserId, bool isAdmin, long memberId, long bookingId, CancellationToken ct = default)
    {
        var userId = await ResolveMemberUserIdAsync(actorUserId, isAdmin, memberId, ct);
        var classId = await classRepository.GetClassIdByBookingIdAsync(bookingId, memberId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt đặt của hội viên.") { Data = { ["StatusCode"] = 404 } };
        await centerScopeService.EnsureClassAccessAsync(actorUserId, isAdmin, classId, ct);
        return await CancelBookingAsync(userId, bookingId, ct, actorUserId);
    }

    public async Task<IReadOnlyList<MemberWaitlistItemDto>> GetMyWaitlistAsync(long userId, CancellationToken ct = default)
    {
        var items = await classRepository.GetMemberWaitlistAsync(userId, ct);
        var now = ScheduleTime.VietnamNow;
        // Entries of sessions that already started or were cancelled can no longer be promoted.
        return items.Where(w => w.SessionStatus == FlowStatuses.SessionScheduled
                && w.SessionDate.ToDateTime(w.StartTime) > now)
            .Select(w => new MemberWaitlistItemDto(
            w.Id,
            w.SessionId,
            w.ClassName,
            w.SessionDate,
            w.StartTime,
            w.EndTime,
            w.Status,
            w.JoinedAt,
            w.Position)).ToList();
    }

    /// <summary>
    /// Fills every free seat of the session from its waitlist (FIFO). Used whenever seats may open up
    /// without a member cancelling (capacity raised, room changed, a missed promotion).
    /// Runs inside the caller's transaction; the caller commits.
    /// </summary>
    public async Task<int> FillOpenSeatsFromWaitlistAsync(long sessionId, long actorUserId, CancellationToken ct = default)
    {
        var session = await Db.ClassSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.SessionStatus != FlowStatuses.SessionScheduled) return 0;
        var cls = await Db.Classes.SingleAsync(c => c.Id == session.ClassId, ct);
        var now = ScheduleTime.VietnamNow;
        if (session.SessionDate.ToDateTime(session.StartTime) <= now) return 0;

        var promoted = 0;
        while (await PromoteNextWaitlistAsync(session, cls, now, actorUserId, ct) is not null)
        {
            promoted++;
            await SaveAsync(ct); // the next round recounts bookings from the database
        }
        await SaveAsync(ct);
        return promoted;
    }

    public async Task LeaveWaitlistAsync(long userId, long waitlistId, CancellationToken ct = default)
    {
        var memberId = await classRepository.GetMemberIdByUserIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy lượt chờ.") { Data = { ["StatusCode"] = 404 } };
        var result = await classRepository.CancelWaitlistEntryAsync(waitlistId, memberId, ct);
        if (result == 0)
            throw new InvalidOperationException("Không tìm thấy lượt chờ.") { Data = { ["StatusCode"] = 404 } };
        if (result == -1)
            throw new InvalidOperationException("Lượt chờ đã được xử lý, vui lòng tải lại lịch.") { Data = { ["StatusCode"] = 409 } };
    }

    public async Task<IReadOnlyList<SessionRosterItemDto>> GetSessionRosterAsync(long coachUserId, long sessionId, CancellationToken ct = default)
    {
        var isCoach = await classRepository.IsCoachAssignedToSessionAsync(coachUserId, sessionId, ct);
        if (!isCoach)
            throw new InvalidOperationException("Bạn không được phân công phụ trách buổi học này.") { Data = { ["StatusCode"] = 403 } };

        var items = await classRepository.GetSessionRosterAsync(sessionId, ct);
        return items.Select(m => new SessionRosterItemDto(
            m.BookingId,
            m.MemberId,
            m.MemberName,
            m.MemberCode,
            m.Email,
            m.Phone,
            m.FitnessGoal,
            m.FitnessLevel,
            m.AttendanceStatus,
            m.Note)).ToList();
    }

    public async Task<int> RecordAttendanceAsync(long coachUserId, long sessionId, IReadOnlyList<AttendanceRecordDto> records, CancellationToken ct = default)
    {
        if (records.Count == 0 || records.Count > 1000 || records.Select(r => r.MemberId).Distinct().Count() != records.Count
            || records.Any(r => r.Status is not ("PRESENT" or "ABSENT" or "LATE" or "EXCUSED") || r.Note?.Length > 500))
            throw new InvalidOperationException("Danh sách điểm danh không hợp lệ.") { Data = { ["StatusCode"] = 400 } };

        var isCoach = await classRepository.IsCoachAssignedToSessionAsync(coachUserId, sessionId, ct);
        if (!isCoach)
            throw new InvalidOperationException("Bạn không được phân công phụ trách buổi học này.") { Data = { ["StatusCode"] = 403 } };

        var session = await classRepository.GetSessionByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("Buổi học không tồn tại.") { Data = { ["StatusCode"] = 404 } };

        var vietnamNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
        if (string.Equals(session.SessionStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ điểm danh khi buổi học đã bắt đầu và không bị hủy.") { Data = { ["StatusCode"] = 409 } };
        if (session.SessionDate < DateOnly.FromDateTime(vietnamNow))
            throw new InvalidOperationException("Buổi học đã qua ngày nên không thể sửa điểm danh.") { Data = { ["StatusCode"] = 409 } };
        if (session.SessionDate.ToDateTime(session.StartTime) > vietnamNow)
            throw new InvalidOperationException("Chỉ điểm danh khi buổi học đã bắt đầu và không bị hủy.") { Data = { ["StatusCode"] = 409 } };

        var ids = records.Select(r => r.MemberId).ToList();
        var eligible = await classRepository.GetEligibleAttendanceMemberIdsAsync(sessionId, session.ClassId, ids, ct);
        if (eligible.Count != records.Count)
            throw new InvalidOperationException("Có học viên không đăng ký buổi học này.") { Data = { ["StatusCode"] = 400 } };

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        await classRepository.UpsertAttendancesAsync(sessionId, coachUserId, records.Select(r => (r.MemberId, r.Status, r.Note)).ToList(), ct);

        var centerId = await classRepository.GetCenterIdByClassIdAsync(session.ClassId, ct);
        var auditRepo = unitOfWork.Repository<AuditLog>();
        await auditRepo.AddAsync(new AuditLog
        {
            UserId = coachUserId,
            CenterId = centerId,
            Action = "SESSION_ATTENDANCE",
            EntityType = "ClassSession",
            EntityId = sessionId,
            CreatedAt = DateTime.UtcNow
        }, ct);

        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return records.Count;
    }

}

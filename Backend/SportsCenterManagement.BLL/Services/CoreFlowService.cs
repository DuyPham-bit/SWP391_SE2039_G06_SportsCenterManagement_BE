using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.DTOs.CoreFlows;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CoreFlowService(IUnitOfWork unitOfWork, IConfiguration configuration) : ICoreFlowService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IConfiguration _configuration = configuration;
    private static readonly TimeSpan EnrollmentCancellationCutoff = TimeSpan.FromHours(2);

    public async Task<IReadOnlyList<MembershipPackage>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Repository<MembershipPackage>()
            .Find(package => package.CenterId == centerId && package.Status == "Active")
            .OrderBy(package => package.Price)
            .ToListAsync(cancellationToken);
    }

    public async Task<PendingMembershipResult> CreatePendingMembershipAsync(
        long memberId,
        long packageId,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        // Serializable ngăn retry đồng thời tạo hai hóa đơn cho cùng member và gói.
        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var member = await _unitOfWork.Repository<MemberProfile>().GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Member was not found.");
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(member.UserId, cancellationToken);
        if (user?.Status != "Active")
        {
            throw new UnauthorizedAccessException("Member account is not active.");
        }

        var package = await _unitOfWork.Repository<MembershipPackage>().Find(
            item => item.Id == packageId)
            .SingleOrDefaultAsync(cancellationToken);
        if (package is null || package.Status != "Active")
        {
            throw new InvalidOperationException("Membership package is unavailable.");
        }

        if (member.CenterId.HasValue && member.CenterId.Value != package.CenterId)
        {
            throw new UnauthorizedAccessException("Membership package belongs to another center.");
        }

        var pending = await (
            from item in _unitOfWork.Context.InvoiceItems
            join pendingInvoice in _unitOfWork.Context.Invoices on item.InvoiceId equals pendingInvoice.Id
            join pendingSubscription in _unitOfWork.Context.MemberSubscriptions on item.SubscriptionId equals pendingSubscription.Id
            where pendingInvoice.MemberId == memberId
                  && item.PackageId == packageId
                  && pendingSubscription.Status == "PendingPayment"
                  && (pendingInvoice.Status == "Issued" || pendingInvoice.Status == "PartiallyPaid")
            orderby pendingInvoice.IssuedAt descending
            select new { Invoice = pendingInvoice, Subscription = pendingSubscription })
            .FirstOrDefaultAsync(cancellationToken);
        if (pending is not null)
        {
            if (!member.CenterId.HasValue)
            {
                member.CenterId = package.CenterId;
                _unitOfWork.Repository<MemberProfile>().Update(member);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            var paidAmount = await _unitOfWork.Repository<Payment>()
                .Find(payment => payment.InvoiceId == pending.Invoice.Id && payment.PaymentStatus == "Succeeded")
                .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
            await transaction.CommitAsync(cancellationToken);
            return new PendingMembershipResult(pending.Subscription.Id, pending.Invoice.Id,
                pending.Invoice.InvoiceNumber, pending.Invoice.TotalAmount - paidAmount);
        }

        var centerIsActive = await _unitOfWork.Repository<Center>()
            .AnyAsync(center => center.Id == package.CenterId && center.Status == "Active", cancellationToken);
        if (!centerIsActive)
        {
            throw new InvalidOperationException("Membership center is unavailable.");
        }

        if (package.Price <= 0 || package.DurationDays <= 0)
        {
            throw new InvalidOperationException("Membership package has an invalid price or duration.");
        }

        var now = DateTime.UtcNow;
        // Gắn member vào center cùng transaction với hóa đơn và subscription.
        if (!member.CenterId.HasValue)
        {
            member.CenterId = package.CenterId;
            member.UpdatedAt = now;
            _unitOfWork.Repository<MemberProfile>().Update(member);
        }

        var subscription = new MemberSubscription
        {
            MemberId = memberId,
            PackageId = package.Id,
            // Chỉ chốt ngày hiệu lực khi payment đủ; thời hạn đã được snapshot tại lúc mua.
            StartDate = null,
            EndDate = null,
            DurationDays = package.DurationDays,
            Price = package.Price,
            Status = "PendingPayment",
            AutoRenew = false,
            CreatedAt = now
        };
        await _unitOfWork.Repository<MemberSubscription>().AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var invoiceNumber = $"SC-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31];
        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            MemberId = memberId,
            CenterId = package.CenterId,
            CreatedBy = createdBy,
            Subtotal = package.Price,
            Discount = 0,
            Tax = 0,
            TotalAmount = package.Price,
            Status = "Issued",
            IssuedAt = now
        };
        await _unitOfWork.Repository<Invoice>().AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _unitOfWork.Repository<InvoiceItem>().AddAsync(new InvoiceItem
        {
            InvoiceId = invoice.Id,
            PackageId = package.Id,
            SubscriptionId = subscription.Id,
            Description = package.Name,
            Quantity = 1,
            UnitPrice = package.Price,
            Amount = package.Price
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLogWriter.Add(_unitOfWork, createdBy ?? user.Id, package.CenterId,
            "membership.payment_requested", "MemberSubscription", subscription.Id,
            newValues: new { PackageId = package.Id, InvoiceId = invoice.Id, invoice.TotalAmount, subscription.DurationDays });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PendingMembershipResult(subscription.Id, invoice.Id, invoiceNumber, package.Price);
    }

    public async Task<ClassEnrollment> EnrollMemberAsync(
        long classId,
        long memberId,
        long subscriptionId,
        long? registeredBy,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var classEntity = await _unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Class was not found.");
        if (classEntity.Status != "Published")
        {
            throw new InvalidOperationException("Class is not open for enrollment.");
        }

        if (classEntity.Capacity <= 0)
        {
            throw new InvalidOperationException("Class capacity is not configured.");
        }

        var member = await _unitOfWork.Repository<MemberProfile>().GetByIdAsync(memberId, cancellationToken)
            ?? throw new KeyNotFoundException("Member was not found.");
        if (!await _unitOfWork.Repository<User>().AnyAsync(
                user => user.Id == member.UserId && user.Status == "Active"
                        && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow), cancellationToken))
        {
            throw new UnauthorizedAccessException("Member account is not active.");
        }
        if (member.CenterId.HasValue && member.CenterId.Value != classEntity.CenterId)
        {
            throw new UnauthorizedAccessException("Member and class belong to different centers.");
        }

        var today = ToBusinessDate(DateTime.UtcNow);
        var subscription = await _unitOfWork.Repository<MemberSubscription>().Find(
                item => item.Id == subscriptionId && item.MemberId == memberId && item.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken);
        if (subscription is null || !subscription.StartDate.HasValue || !subscription.EndDate.HasValue
            || subscription.StartDate.Value > today || subscription.EndDate.Value < today)
        {
            throw new InvalidOperationException("An active membership is required to enroll.");
        }

        var membershipPackage = await _unitOfWork.Repository<MembershipPackage>().Find(
            package => package.Id == subscription.PackageId)
            .SingleAsync(cancellationToken);
        if (membershipPackage.CenterId != classEntity.CenterId)
        {
            throw new InvalidOperationException("Membership and class belong to different centers.");
        }

        if (member.CenterId.HasValue && member.CenterId.Value != membershipPackage.CenterId)
        {
            throw new UnauthorizedAccessException("Member and membership package belong to different centers.");
        }

        if (membershipPackage.MaxClasses is int maxClasses)
        {
            var activeClassCount = await _unitOfWork.Repository<ClassEnrollment>().CountAsync(
                enrollment => enrollment.SubscriptionId == subscription.Id && enrollment.Status == "Confirmed",
                cancellationToken);
            var isAlreadyEnrolled = await _unitOfWork.Repository<ClassEnrollment>().AnyAsync(
                enrollment => enrollment.ClassId == classId && enrollment.MemberId == memberId && enrollment.Status == "Confirmed",
                cancellationToken);
            if (!isAlreadyEnrolled && activeClassCount >= maxClasses)
            {
                throw new InvalidOperationException("Membership class allowance has been reached.");
            }
        }

        var existing = await _unitOfWork.Repository<ClassEnrollment>().Find(
            enrollment => enrollment.ClassId == classId && enrollment.MemberId == memberId)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null && existing.Status == "Confirmed")
        {
            throw new InvalidOperationException("Member is already enrolled in this class.");
        }

        var confirmedCount = await _unitOfWork.Repository<ClassEnrollment>().CountAsync(
            enrollment => enrollment.ClassId == classId && enrollment.Status == "Confirmed",
            cancellationToken);
        if (confirmedCount >= classEntity.Capacity)
        {
            throw new InvalidOperationException("Class is full.");
        }

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            existing = new ClassEnrollment
            {
                ClassId = classId,
                MemberId = memberId,
                RegisteredAt = now
            };
            await _unitOfWork.Repository<ClassEnrollment>().AddAsync(existing, cancellationToken);
        }

        existing.SubscriptionId = subscription.Id;
        existing.RegisteredBy = registeredBy;
        existing.RegisteredAt = now;
        existing.CancelledAt = null;
        existing.CancellationReason = null;
        existing.Status = "Confirmed";
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return existing;
    }

    public async Task<ClassEnrollment> CancelClassEnrollmentAsync(long classId, long memberId, string? reason, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var enrollment = await _unitOfWork.Repository<ClassEnrollment>().Find(x => x.ClassId == classId && x.MemberId == memberId)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Không tìm thấy ghi danh của Member trong lớp này.");
        if (enrollment.Status != "Confirmed") throw new InvalidOperationException("Ghi danh không ở trạng thái có thể hủy.");

        // Schedules are expressed in the center's local time; current centers use Vietnam time (UTC+7).
        var nowLocal = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
        var upcomingSessions = await _unitOfWork.Context.ClassSessions
            .Where(x => x.ClassId == classId && x.SessionStatus == "Scheduled")
            .OrderBy(x => x.SessionDate).ThenBy(x => x.StartTime)
            .Select(x => new { x.SessionDate, x.StartTime }).ToListAsync(cancellationToken);
        var nextSession = upcomingSessions.FirstOrDefault(x => x.SessionDate.ToDateTime(x.StartTime) > nowLocal);
        if (nextSession is null) throw new InvalidOperationException("Lớp không còn buổi học có thể hủy ghi danh.");
        if (nowLocal >= nextSession.SessionDate.ToDateTime(nextSession.StartTime) - EnrollmentCancellationCutoff)
            throw new InvalidOperationException("Chỉ được hủy ghi danh trước giờ học ít nhất 2 tiếng.");

        enrollment.Status = "Cancelled";
        enrollment.CancelledAt = DateTime.UtcNow;
        enrollment.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return enrollment;
    }

    public async Task<Payment> RecordCashPaymentAsync(
        long invoiceId,
        long processedBy,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0 || amount > 9_999_999_999.99m || decimal.Round(amount, 2) != amount)
        {
            throw new ValidationException("Số tiền phải lớn hơn 0, tối đa 2 chữ số thập phân và nằm trong giới hạn hóa đơn.");
        }
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException("Idempotency key là bắt buộc.");
        }
        var normalizedKey = idempotencyKey.Trim();
        if (normalizedKey.Length is < 16 or > 100)
        {
            throw new ValidationException("Idempotency key cần từ 16 đến 100 ký tự.");
        }

        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey)));
        var transactionCode = $"CASH-{keyHash}";

        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var previousAttempt = await _unitOfWork.Repository<Payment>()
            .Find(payment => payment.TransactionCode == transactionCode)
            .SingleOrDefaultAsync(cancellationToken);
        if (previousAttempt is not null)
        {
            if (previousAttempt.InvoiceId != invoiceId || previousAttempt.Amount != amount
                || previousAttempt.PaymentStatus != "Succeeded")
            {
                throw new InvalidOperationException("Idempotency key was already used for a different payment request.");
            }
            await transaction.CommitAsync(cancellationToken);
            return previousAttempt;
        }

        var invoice = await _unitOfWork.Repository<Invoice>().GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice was not found.");
        if (invoice.Status is not ("Issued" or "PartiallyPaid"))
        {
            throw new InvalidOperationException("Invoice cannot accept another payment.");
        }

        var paidAmount = await _unitOfWork.Repository<Payment>()
            .Find(payment => payment.InvoiceId == invoiceId && payment.PaymentStatus == "Succeeded")
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var remaining = invoice.TotalAmount - paidAmount;
        if (amount > remaining)
        {
            throw new InvalidOperationException("Payment exceeds the outstanding invoice balance.");
        }

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = invoice.MemberId,
            ProcessedBy = processedBy,
            PaymentMethod = "Cash",
            TransactionCode = transactionCode,
            Amount = amount,
            PaymentStatus = "Succeeded",
            AmountReceived = amount,
            CreatedAt = now,
            PaidAt = now
        };
        await _unitOfWork.Repository<Payment>().AddAsync(payment, cancellationToken);

        var newPaidAmount = paidAmount + amount;
        invoice.Status = newPaidAmount == invoice.TotalAmount ? "Paid" : "PartiallyPaid";
        invoice.PaidAt = invoice.Status == "Paid" ? now : null;

        if (invoice.Status == "Paid")
        {
            var db = _unitOfWork.Context;
            var subscriptions = await (
                from item in db.InvoiceItems
                join sub in db.MemberSubscriptions on item.SubscriptionId equals sub.Id
                where item.InvoiceId == invoice.Id
                select sub).ToListAsync(cancellationToken);
            foreach (var subscription in subscriptions)
            {
                if (subscription.Status != "PendingPayment")
                {
                    continue;
                }
                var durationDays = subscription.DurationDays;
                if (durationDays <= 0)
                {
                    var package = await _unitOfWork.Repository<MembershipPackage>().Find(
                        item => item.Id == subscription.PackageId)
                        .SingleOrDefaultAsync(cancellationToken);
                    durationDays = package?.DurationDays ?? 30;
                }
                var today = ToBusinessDate(now);
                var currentEnd = await _unitOfWork.Repository<MemberSubscription>()
                    .Find(item => item.MemberId == subscription.MemberId && item.Status == "Active" && item.EndDate >= today)
                    .MaxAsync(item => (DateOnly?)item.EndDate, cancellationToken);
                subscription.StartDate = currentEnd.HasValue ? currentEnd.Value.AddDays(1) : today;
                subscription.EndDate = subscription.StartDate.Value.AddDays(durationDays - 1);
                subscription.Status = "Active";
                subscription.UpdatedAt = now;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(_unitOfWork, processedBy, invoice.CenterId, "payment.cash.recorded", "Payment", payment.Id,
            newValues: new { InvoiceId = invoice.Id, PaymentAmount = amount, InvoiceStatus = invoice.Status, IdempotencyKeyHash = keyHash });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return payment;
    }

    public async Task<RevenueSummary> GetRevenueAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 366)
        {
            throw new InvalidOperationException("The report date range must be valid and no longer than 367 days.");
        }

        var timeZone = GetReportTimeZone();
        var startLocal = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var start = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endExclusive = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
        var db = _unitOfWork.Context;
        var gross = await (
            from payment in db.Payments
            join invoice in db.Invoices on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId
                && payment.PaymentStatus == "Succeeded"
                && payment.PaidAt.HasValue
                && payment.PaidAt.Value >= start && payment.PaidAt.Value < endExclusive
            select (decimal?)payment.Amount).SumAsync(cancellationToken) ?? 0m;

        var refunds = await (
            from refund in db.PaymentRefunds
            join payment in db.Payments on refund.PaymentId equals payment.Id
            join invoice in db.Invoices on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId
                && refund.Status == "Succeeded"
                && refund.ProcessedAt >= start && refund.ProcessedAt < endExclusive
            select (decimal?)refund.Amount).SumAsync(cancellationToken) ?? 0m;

        return new RevenueSummary(centerId, from, to, gross, refunds, gross - refunds);
    }

    private TimeZoneInfo GetReportTimeZone()
    {
        var configured = _configuration["Reports:TimeZoneId"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configured);
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private DateOnly ToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), GetReportTimeZone()));
}

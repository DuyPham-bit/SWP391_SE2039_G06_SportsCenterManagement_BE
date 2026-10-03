using System.Data;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.CoreFlows;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CoreFlowService(IUnitOfWork unitOfWork) : ICoreFlowService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
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
        var memberExists = await _unitOfWork.Repository<MemberProfile>().AnyAsync(member => member.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            throw new InvalidOperationException("Member was not found.");
        }

        var package = await _unitOfWork.Repository<MembershipPackage>().Find(
            item => item.Id == packageId && item.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken);
        if (package is null)
        {
            throw new InvalidOperationException("Membership package is unavailable.");
        }

        if (package.Price < 0 || package.DurationDays <= 0)
        {
            throw new InvalidOperationException("Membership package has an invalid price or duration.");
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var subscription = new MemberSubscription
        {
            MemberId = memberId,
            PackageId = package.Id,
            StartDate = today,
            EndDate = today.AddDays(package.DurationDays),
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subscription = await _unitOfWork.Repository<MemberSubscription>().Find(
                item => item.Id == subscriptionId && item.MemberId == memberId && item.Status == "Active")
            .SingleOrDefaultAsync(cancellationToken);
        if (subscription is null || subscription.StartDate > today || subscription.EndDate < today)
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

        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        await using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await _unitOfWork.Repository<Invoice>().GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice was not found.");
        if (invoice.Status is "Paid" or "Voided" or "Refunded")
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
            TransactionCode = $"CASH-{Guid.NewGuid():N}",
            Amount = amount,
            PaymentStatus = "Succeeded",
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
                join subscription in db.MemberSubscriptions on item.SubscriptionId equals subscription.Id
                where item.InvoiceId == invoice.Id
                select subscription).ToListAsync(cancellationToken);
            foreach (var subscription in subscriptions)
            {
                var package = await _unitOfWork.Repository<MembershipPackage>().Find(
                    item => item.Id == subscription.PackageId)
                    .SingleAsync(cancellationToken);
                var today = DateOnly.FromDateTime(now);
                var currentEnd = await _unitOfWork.Repository<MemberSubscription>()
                    .Find(item => item.MemberId == subscription.MemberId && item.Status == "Active" && item.EndDate >= today)
                    .MaxAsync(item => (DateOnly?)item.EndDate, cancellationToken);
                subscription.StartDate = currentEnd.HasValue ? currentEnd.Value.AddDays(1) : today;
                subscription.EndDate = subscription.StartDate.AddDays(package.DurationDays);
                subscription.Status = "Active";
                subscription.UpdatedAt = now;
            }
        }

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
        if (from > to)
        {
            throw new InvalidOperationException("Start date must be on or before end date.");
        }

        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var db = _unitOfWork.Context;
        var gross = await (
            from payment in db.Payments
            join invoice in db.Invoices on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId
                && payment.PaymentStatus == "Succeeded"
                && payment.PaidAt >= start && payment.PaidAt < endExclusive
            select (decimal?)payment.Amount).SumAsync(cancellationToken) ?? 0m;

        return new RevenueSummary(centerId, from, to, gross, 0m, gross);
    }
}

using System.Data;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.Models;

namespace SportsCenterManagement.Services.Features.CoreFlows;

public sealed record PendingMembershipResult(long SubscriptionId, long InvoiceId, string InvoiceNumber, decimal Amount);

public sealed record RevenueSummary(long CenterId, DateOnly From, DateOnly To, decimal Gross, decimal Refunds, decimal Net);

/// <summary>
/// Application use cases for the required membership, class enrollment and cashier/report flows.
/// HTTP controllers must authenticate the caller and enforce center/role scope before calling these methods.
/// </summary>
public sealed class CoreFlowService(SportsCenterDbContext db)
{
    public async Task<IReadOnlyList<MembershipPackage>> GetActivePackagesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
    {
        return await db.MembershipPackages.AsNoTracking()
            .Where(package => package.CenterId == centerId && package.Status == "Active")
            .OrderBy(package => package.Price)
            .ToListAsync(cancellationToken);
    }

    public async Task<PendingMembershipResult> CreatePendingMembershipAsync(
        long memberId,
        long packageId,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        var memberExists = await db.MemberProfiles.AnyAsync(member => member.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            throw new InvalidOperationException("Member was not found.");
        }

        var package = await db.MembershipPackages.SingleOrDefaultAsync(
            item => item.Id == packageId && item.Status == "Active",
            cancellationToken);
        if (package is null)
        {
            throw new InvalidOperationException("Membership package is unavailable.");
        }

        if (package.Price < 0 || package.DurationDays <= 0)
        {
            throw new InvalidOperationException("Membership package has an invalid price or duration.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
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
        db.MemberSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);

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
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        db.InvoiceItems.Add(new InvoiceItem
        {
            InvoiceId = invoice.Id,
            PackageId = package.Id,
            SubscriptionId = subscription.Id,
            Description = package.Name,
            Quantity = 1,
            UnitPrice = package.Price,
            Amount = package.Price
        });
        await db.SaveChangesAsync(cancellationToken);
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
        var classEntity = await db.Classes.SingleOrDefaultAsync(item => item.Id == classId, cancellationToken)
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
        var subscription = await db.MemberSubscriptions.SingleOrDefaultAsync(
                item => item.Id == subscriptionId && item.MemberId == memberId && item.Status == "Active",
                cancellationToken);
        if (subscription is null || subscription.StartDate > today || subscription.EndDate < today)
        {
            throw new InvalidOperationException("An active membership is required to enroll.");
        }

        var membershipPackage = await db.MembershipPackages.SingleAsync(
            package => package.Id == subscription.PackageId,
            cancellationToken);
        if (membershipPackage.CenterId != classEntity.CenterId)
        {
            throw new InvalidOperationException("Membership and class belong to different centers.");
        }

        if (membershipPackage.MaxClasses is int maxClasses)
        {
            var activeClassCount = await db.ClassEnrollments.CountAsync(
                enrollment => enrollment.SubscriptionId == subscription.Id && enrollment.Status == "Confirmed",
                cancellationToken);
            var isAlreadyEnrolled = await db.ClassEnrollments.AnyAsync(
                enrollment => enrollment.ClassId == classId && enrollment.MemberId == memberId && enrollment.Status == "Confirmed",
                cancellationToken);
            if (!isAlreadyEnrolled && activeClassCount >= maxClasses)
            {
                throw new InvalidOperationException("Membership class allowance has been reached.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await db.ClassEnrollments.SingleOrDefaultAsync(
            enrollment => enrollment.ClassId == classId && enrollment.MemberId == memberId,
            cancellationToken);
        if (existing is not null && existing.Status == "Confirmed")
        {
            throw new InvalidOperationException("Member is already enrolled in this class.");
        }

        var confirmedCount = await db.ClassEnrollments.CountAsync(
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
            db.ClassEnrollments.Add(existing);
        }

        existing.SubscriptionId = subscription.Id;
        existing.RegisteredBy = registeredBy;
        existing.RegisteredAt = now;
        existing.CancelledAt = null;
        existing.CancellationReason = null;
        existing.Status = "Confirmed";
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return existing;
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

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await db.Invoices.SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice was not found.");
        if (invoice.Status is "Paid" or "Voided" or "Refunded")
        {
            throw new InvalidOperationException("Invoice cannot accept another payment.");
        }

        var paidAmount = await db.Payments
            .Where(payment => payment.InvoiceId == invoiceId && payment.PaymentStatus == "Succeeded")
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
        db.Payments.Add(payment);

        var newPaidAmount = paidAmount + amount;
        invoice.Status = newPaidAmount == invoice.TotalAmount ? "Paid" : "PartiallyPaid";
        invoice.PaidAt = invoice.Status == "Paid" ? now : null;

        if (invoice.Status == "Paid")
        {
            var subscriptions = await (
                from item in db.InvoiceItems
                join subscription in db.MemberSubscriptions on item.SubscriptionId equals subscription.Id
                where item.InvoiceId == invoice.Id
                select subscription).ToListAsync(cancellationToken);
            foreach (var subscription in subscriptions)
            {
                var package = await db.MembershipPackages.SingleAsync(
                    item => item.Id == subscription.PackageId,
                    cancellationToken);
                var today = DateOnly.FromDateTime(now);
                var currentEnd = await db.MemberSubscriptions
                    .Where(item => item.MemberId == subscription.MemberId && item.Status == "Active" && item.EndDate >= today)
                    .MaxAsync(item => (DateOnly?)item.EndDate, cancellationToken);
                subscription.StartDate = currentEnd.HasValue ? currentEnd.Value.AddDays(1) : today;
                subscription.EndDate = subscription.StartDate.AddDays(package.DurationDays);
                subscription.Status = "Active";
                subscription.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
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
        var gross = await (
            from payment in db.Payments
            join invoice in db.Invoices on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId
                && payment.PaymentStatus == "Succeeded"
                && payment.PaidAt >= start && payment.PaidAt < endExclusive
            select (decimal?)payment.Amount).SumAsync(cancellationToken) ?? 0m;

        // Refund persistence is not modeled as a separate transaction yet; the value remains zero until that flow is defined.
        return new RevenueSummary(centerId, from, to, gross, 0m, gross);
    }
}

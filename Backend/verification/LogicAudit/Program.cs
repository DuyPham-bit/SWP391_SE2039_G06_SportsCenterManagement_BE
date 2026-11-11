using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.API.Controllers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.DTOs.CoachProfessional;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.BLL.Services.Interfaces;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var name = "scms_logicaudit_" + Guid.NewGuid().ToString("N");
var defaultPassword = "73cfbcd74a8a6c226c50f8257d3a6bff140432857ca86452";
var auditPgPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
if (string.IsNullOrWhiteSpace(auditPgPassword))
{
    try
    {
        var envCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docker/.env")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "docker/.env"))
        };
        foreach (var envFile in envCandidates)
        {
            if (File.Exists(envFile))
            {
                foreach (var line in File.ReadAllLines(envFile))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("POSTGRES_PASSWORD="))
                    {
                        auditPgPassword = trimmed.Substring("POSTGRES_PASSWORD=".Length).Trim('"', '\'');
                        break;
                    }
                }
                if (!string.IsNullOrWhiteSpace(auditPgPassword)) break;
            }
        }
    }
    catch { }
}
var pgPass = !string.IsNullOrWhiteSpace(auditPgPassword) ? auditPgPassword : defaultPassword;
var serverConnection = Environment.GetEnvironmentVariable("SCMS_AUDIT_PG_CONNECTION")
    ?? $"Host=localhost;Port=5432;Database=postgres;Username=postgres;Password={pgPass}";
var master = new Npgsql.NpgsqlConnection(serverConnection);
await master.OpenAsync();
await using (var command = master.CreateCommand()) {
    command.CommandText = $"CREATE DATABASE \"{name}\"";
    await command.ExecuteNonQueryAsync();
}
var auditConnection = new Npgsql.NpgsqlConnectionStringBuilder(serverConnection) { Database = name }.ConnectionString;
var options = new DbContextOptionsBuilder<SportsCenterDbContext>().UseNpgsql(auditConnection).Options;
var failures = new List<string>();
int count = 0;
try {
    await using (var db = new SportsCenterDbContext(options)) {
        await db.Database.MigrateAsync();
        var center = new Center { Name = "Test center", Address = "Test", Status = "Active", CreatedAt = DateTime.UtcNow };
        var other = new Center { Name = "Other center", Address = "Test", Status = "Active", CreatedAt = DateTime.UtcNow };
        var memberRole = new Role { Name = "Member" }; var coachRole = new Role { Name = "Coach" }; var staffRole = new Role { Name = "Receptionist" };
        db.AddRange(center, other, memberRole, coachRole, staffRole); await db.SaveChangesAsync();
        User User(string username, long roleId) => new() { Username = username, Email = username + "@example.test", PasswordHash = "test-only", RoleId = roleId, Status = "Active", CreatedAt = DateTime.UtcNow };
        var owner = User("owner", memberRole.Id); var coach = User("coach", coachRole.Id); var staff = User("staff", staffRole.Id);
        db.AddRange(owner, coach, staff); await db.SaveChangesAsync();
        var member = new MemberProfile { UserId = owner.Id, CenterId = center.Id, MemberCode = "AUDIT-MEMBER", FullName = "Test member", CreatedAt = DateTime.UtcNow };
        var package = new MembershipPackage { CenterId = center.Id, Name = "Test package", DurationDays = 90, Price = 1000000, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.AddRange(member, package, new StaffProfile { UserId = staff.Id, CenterId = other.Id, StaffCode = "AUDIT-STAFF", FullName = "Other staff", Position = "Receptionist", Status = "Active", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var unassignedMemberUser = User("unassigned-member", memberRole.Id);
        db.Users.Add(unassignedMemberUser); await db.SaveChangesAsync();
        var unassignedMember = new MemberProfile { UserId = unassignedMemberUser.Id, MemberCode = "AUDIT-UNASSIGNED", FullName = "Unassigned member", CreatedAt = DateTime.UtcNow };
        db.MemberProfiles.Add(unassignedMember); await db.SaveChangesAsync();
        var core = new CoreFlowService(new UnitOfWork(db), new ConfigurationBuilder().Build());
        var pending = await core.CreatePendingMembershipAsync(member.Id, package.Id, owner.Id);
        Console.WriteLine($"FIXTURE {name}");
        HttpClient? apiClient = null;
        async Task Check(string label, Func<Task<bool>> action) {
            count++;
            apiClient?.DefaultRequestHeaders.Remove("X-Audit-Client");
            apiClient?.DefaultRequestHeaders.Add("X-Audit-Client", $"127.1.{count / 250}.{count % 250 + 1}");
            try { if (!await action()) throw new Exception("Assertion failed"); Console.WriteLine("PASS " + label); }
            catch(Exception e) { failures.Add(label); Console.WriteLine("FAIL " + label + ": " + e.Message); }
            db.ChangeTracker.Clear();
        }
        await Check("unauthorized counter sale cannot assign an unowned member to a center", async () => {
            try
            {
                await new MemberService(new UnitOfWork(db), null!, new BusinessClock(new ConfigurationBuilder().Build()))
                    .EnsureCanSellToMemberAsync(staff.Id, unassignedMember.Id, package.Id);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                db.ChangeTracker.Clear();
                return await db.MemberProfiles.AsNoTracking()
                    .Where(profile => profile.Id == unassignedMember.Id)
                    .Select(profile => profile.CenterId)
                    .SingleAsync() is null;
            }
        });
        async Task<bool> Forbidden(Func<PaymentService, Task> action) {
            try { await action(new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build())); return false; }
            catch (BusinessException e) { return e.StatusCode == HttpStatusCode.Forbidden; }
        }
        await Check("coach cannot cancel another member invoice", () => Forbidden(s => s.CancelPendingInvoiceAsync(coach.Id, pending.InvoiceNumber)));
        // Restore the isolated fixture after exercising the vulnerable baseline.
        await db.Invoices.Where(i => i.Id == pending.InvoiceId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "Issued"));
        await db.MemberSubscriptions.Where(s => s.Id == pending.SubscriptionId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "PendingPayment"));
        await Check("staff cannot cancel an invoice from another center", () => Forbidden(s => s.CancelPendingInvoiceAsync(staff.Id, pending.InvoiceNumber)));
        await db.Invoices.Where(i => i.Id == pending.InvoiceId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "Issued"));
        await db.MemberSubscriptions.Where(s => s.Id == pending.SubscriptionId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "PendingPayment"));
        await Check("staff must specify a member when cancelling by package", async () => {
            try { await new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build()).CancelPendingPackageAsync(staff.Id, package.Id); return false; }
            catch (BusinessException e) { return e.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden; }
        });
        await db.Invoices.Where(i => i.Id == pending.InvoiceId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "PartiallyPaid"));
        await db.MemberSubscriptions.Where(s => s.Id == pending.SubscriptionId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "PendingPayment"));
        db.Payments.Add(new Payment { InvoiceId = pending.InvoiceId, MemberId = member.Id, PaymentMethod = "CASH", Amount = 250000, PaymentStatus = "Succeeded", PaidAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Check("pending invoice keeps purchased duration and outstanding amount", async () => {
            await db.MembershipPackages.Where(p => p.Id == package.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.DurationDays, 180));
            var list = await new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build()).GetMyPendingInvoicesAsync(owner.Id);
            return list.Count == 1 && list[0].DurationDays == 90 && list[0].Amount == 750000;
        });
        await Check("replacement checkout cannot void an invoice with a successful partial payment", async () => {
            try {
                await new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build()).ProcessCounterPaymentAsync(staff.Id, center.Id, Guid.NewGuid().ToString(), new CounterPaymentRequest { MemberId = member.Id, PackageId = package.Id, AmountReceived = 1000000, PaymentMethod = "CASH", CancelPendingIfAny = true });
                return false;
            } catch (BusinessException e) { return e.StatusCode == HttpStatusCode.Conflict; }
        });

        var selfPay = new Permission { Code = PermissionCodes.PaymentSelfCreate, Name = "Self payment" };
        var counterPay = new Permission { Code = PermissionCodes.PaymentCenterCash, Name = "Counter payment" };
        var selfRead = new Permission { Code = PermissionCodes.MemberSelfRead, Name = "Read self" };
        var subRead = new Permission { Code = PermissionCodes.SubscriptionRead, Name = "Read subscriptions" };
        db.AddRange(selfPay, counterPay, selfRead, subRead); await db.SaveChangesAsync();
        db.AddRange(new RolePermission { RoleId = memberRole.Id, PermissionId = selfPay.Id },
            new RolePermission { RoleId = memberRole.Id, PermissionId = selfRead.Id },
            new RolePermission { RoleId = memberRole.Id, PermissionId = subRead.Id },
            new RolePermission { RoleId = staffRole.Id, PermissionId = counterPay.Id });
        var secondPackage = new MembershipPackage { CenterId = center.Id, Name = "Second package", DurationDays = 30, Price = 500000, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Add(secondPackage); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var personal = await core.CreatePendingMembershipAsync(member.Id, secondPackage.Id, owner.Id);
        await Check("owner may cancel own unpaid invoice", async () => {
            var result = await new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build()).CancelPendingInvoiceAsync(owner.Id, personal.InvoiceNumber);
            return result.Success && await db.Invoices.AnyAsync(i => i.Id == personal.InvoiceId && i.Status == "Voided")
                && await db.MemberSubscriptions.AnyAsync(s => s.Id == personal.SubscriptionId && s.Status == "Cancelled")
                && await db.AuditLogs.AnyAsync(a => a.EntityType == "Invoice" && a.EntityId == personal.InvoiceId && a.CenterId == center.Id);
        });
        await Check("same-center staff may cancel a selected unpaid invoice", async () => {
            await db.StaffProfiles.Where(s => s.UserId == staff.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.CenterId, center.Id));
            var pending2 = await core.CreatePendingMembershipAsync(member.Id, secondPackage.Id, owner.Id);
            var result = await new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build()).CancelPendingPackageAsync(staff.Id, secondPackage.Id, member.Id);
            return result.Success && await db.Invoices.AnyAsync(i => i.Id == pending2.InvoiceId && i.Status == "Voided");
        });
        await Check("center permission revocation prevents staff cancellation", async () => {
            db.SystemSettings.Add(new SystemSetting { SettingKey = CenterRolePolicy.Key(center.Id, staffRole.Id), SettingValue = JsonSerializer.Serialize(new CenterRoleOverride([], [])) });
            await db.SaveChangesAsync();
            return await Forbidden(s => s.CancelPendingInvoiceAsync(staff.Id, pending.InvoiceNumber));
        });
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        db.MemberSubscriptions.Add(new MemberSubscription { MemberId = member.Id, PackageId = package.Id,
            Price = 1000000, DurationDays = 90, Status = "Active", StartDate = today.AddDays(1), EndDate = today.AddDays(90), CreatedAt = DateTime.UtcNow });
        var sport = new Sport { Name = "Test sport", Status = "Active" };
        var room = new Room { CenterId = center.Id, Name = "Actual room name", Capacity = 20, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.AddRange(sport, room); await db.SaveChangesAsync();
        var cls = new ClassEntity { CenterId = center.Id, SportId = sport.Id, RoomId = room.Id, Name = "Own class", Capacity = 10, DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow };
        var foreign = new ClassEntity { CenterId = other.Id, SportId = sport.Id, Name = "Foreign class", Capacity = 10, DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow };
        db.AddRange(cls, foreign); await db.SaveChangesAsync();
        db.AddRange(new ClassSession { ClassId = cls.Id, SessionDate = today, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(19, 0), SessionStatus = "Scheduled" },
            new ClassSession { ClassId = foreign.Id, SessionDate = today, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(19, 0), SessionStatus = "Scheduled" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Check("backend classifies scheduled membership using business date", async () => {
            var list = await new MemberService(
                new UnitOfWork(db),
                null!,
                new BusinessClock(new ConfigurationBuilder().Build()))
                .GetSubscriptionsAsync(owner.Id, member.Id);
            return list.Any(s => s.Status == "Active" && s.EffectiveStatus == "SCHEDULED");
        });

        var profile = new CoachProfile { UserId = coach.Id, CenterId = center.Id, CoachCode = "AUDIT-COACH", FullName = "Test coach", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Add(profile); await db.SaveChangesAsync();
        db.Add(new ClassCoach { ClassId = cls.Id, CoachId = profile.Id, IsPrimary = true });
        db.Add(new ClassSession { ClassId = cls.Id, SessionDate = today, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0), SessionStatus = "Scheduled" });
        db.Add(new ClassSession { ClassId = cls.Id, SessionDate = today.AddDays(2), StartTime = new TimeOnly(0, 30), EndTime = new TimeOnly(1, 30), SessionStatus = "Scheduled" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var clock = new FixedTimeProvider(new DateTimeOffset(today.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc)));
        var professional = new CoachProfessionalService(db, clock);
        await Check("coach KPI excludes future sessions from completion and income", async () => {
            var json = JsonSerializer.SerializeToElement(await professional.GetKpi(coach.Id, today.Year, today.Month, default));
            return json.GetProperty("CompletedSessions").GetInt32() == 1;
        });
        await Check("coach event crossing midnight cannot overlap next-day class", async () => {
            try { await professional.CreateWorkEvent(coach.Id, new SaveWorkEventRequest(null, null, "Overnight conflict", "Other",
                today.AddDays(1).ToDateTime(new TimeOnly(23, 0)), today.AddDays(2).ToDateTime(new TimeOnly(1, 0)), null), default); return false; }
            catch (BusinessException e) { return e.StatusCode == HttpStatusCode.Conflict; }
        });
        var cancelledEvent = new CoachWorkEvent { CenterId = center.Id, CoachId = profile.Id, Title = "Cancelled event", EventType = "Other",
            StartAt = clock.GetUtcNow().UtcDateTime.AddDays(-2), EndAt = clock.GetUtcNow().UtcDateTime.AddDays(-2).AddHours(1), Status = "Cancelled", CreatedBy = coach.Id, CreatedAt = DateTime.UtcNow };
        var declinedEvent = new CoachWorkEvent { CenterId = center.Id, CoachId = profile.Id, Title = "Declined event", EventType = "Other",
            StartAt = today.AddDays(1).ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc), EndAt = today.AddDays(1).ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc), Status = "Declined", CreatedBy = coach.Id, CreatedAt = DateTime.UtcNow };
        db.AddRange(cancelledEvent, declinedEvent); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Check("cancelled coach event cannot be resurrected as completed", async () => {
            try { await professional.ChangeWorkEvent(coach.Id, cancelledEvent.Id, new ChangeWorkEventStatusRequest("Completed", null), false, default); return false; }
            catch (BusinessException e) { return e.StatusCode == HttpStatusCode.Conflict; }
        });
        await Check("declined appointment releases the coach time slot", async () => {
            return await professional.CreateWorkEvent(coach.Id, new SaveWorkEventRequest(null, null, "Available slot", "Other",
                today.AddDays(1).ToDateTime(new TimeOnly(14, 0)), today.AddDays(1).ToDateTime(new TimeOnly(15, 0)), null), default) > 0;
        });
        var managerRole = new Role { Name = "Manager" }; db.Add(managerRole); await db.SaveChangesAsync();
        var manager = User("manager", managerRole.Id); var foreignUser = User("foreign-member", memberRole.Id); db.AddRange(manager, foreignUser); await db.SaveChangesAsync();
        var foreignMember = new MemberProfile { UserId = foreignUser.Id, CenterId = other.Id, MemberCode = "AUDIT-FOREIGN", FullName = "Foreign", CreatedAt = DateTime.UtcNow };
        var foreignPackage = new MembershipPackage { CenterId = other.Id, Name = "Foreign package", DurationDays = 30, Price = 500000, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.AddRange(foreignMember, foreignPackage, new StaffProfile { UserId = manager.Id, CenterId = center.Id, StaffCode = "AUDIT-MANAGER", FullName = "Manager", Position = "Manager", Status = "Active", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var foreignSubscription = new MemberSubscription { MemberId = foreignMember.Id, PackageId = foreignPackage.Id, DurationDays = 30, Price = 500000, Status = "Active", StartDate = today, EndDate = today.AddDays(29), CreatedAt = DateTime.UtcNow };
        db.Add(foreignSubscription); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Check("sales credit cannot link a subscription from another center", async () => {
            try { await professional.SaveSalesCredit(manager.Id, new SaveSalesCreditRequest(profile.Id, null, foreignSubscription.Id, 500000, null, null), default); return false; }
            catch (BusinessException e) { return e.StatusCode == HttpStatusCode.BadRequest; }
        });
        var reports = new ReportService(new UnitOfWork(db), null!, new ConfigurationBuilder().Build());
        await Check("pending and scheduled subscriptions are not expired members", async () => {
            var report = await reports.GetMembershipReportAsync(center.Id, today, today, "day");
            return report.ExpiredMembers == 0;
        });
        await Check("membership revenue uses collected payments, not pending subscription prices", async () => {
            var report = await reports.GetMembershipReportAsync(center.Id, today, today, "day");
            var item = report.Packages!.Single(p => p.Id == package.Id);
            return item.SoldInPeriod == 0 && item.RevenueInPeriod == 250000;
        });
        var scheduled = await db.MemberSubscriptions.SingleAsync(s => s.MemberId == member.Id && s.Status == "Active");
        var bookedSession = new ClassSession { ClassId = cls.Id, SessionDate = today.AddDays(1), StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(17, 0), SessionStatus = "Scheduled" };
        db.Add(bookedSession); await db.SaveChangesAsync();
        db.Add(new SessionBooking { MemberId = member.Id, SessionId = bookedSession.Id, SubscriptionId = scheduled.Id, Status = "Booked", BookedAt = DateTime.UtcNow });
        var paidInvoice = new Invoice { InvoiceNumber = "SC-AUDIT-PAID", MemberId = member.Id, CenterId = center.Id, CreatedBy = owner.Id, Subtotal = 1000000, TotalAmount = 1000000, Status = "Paid", IssuedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow };
        db.Add(paidInvoice); await db.SaveChangesAsync();
        db.AddRange(new InvoiceItem { InvoiceId = paidInvoice.Id, PackageId = package.Id, SubscriptionId = scheduled.Id, Description = "Paid test package", Quantity = 1, UnitPrice = 1000000, Amount = 1000000 },
            new Payment { InvoiceId = paidInvoice.Id, MemberId = member.Id, PaymentMethod = "CASH", Amount = 1000000, PaymentStatus = "Succeeded", PaidAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Check("class reports include per-session bookings", async () => {
            var report = await reports.GetClassEnrollmentReportAsync(center.Id, today, today, "day");
            return report.CurrentStatusCounts.GetValueOrDefault("Booked") == 1;
        });
        await Check("sport revenue can trace a paid subscription through session bookings", async () => {
            var report = await reports.GetSportRevenueReportAsync(center.Id, today, today);
            return report.AllocatedNet == 1000000 && report.UnallocatedNet == 250000;
        });
        await Check("membership refund is recognized in its processing period", async () => {
            var paymentId = await db.Payments.Where(p => p.InvoiceId == paidInvoice.Id).Select(p => p.Id).SingleAsync();
            var processedAt = DateTime.UtcNow.AddDays(32);
            db.PaymentRefunds.Add(new PaymentRefund { PaymentId = paymentId, Amount = 100000, Status = "Succeeded", IdempotencyKey = Guid.NewGuid().ToString(), Reason = "Audit refund", RequestedBy = manager.Id, CreatedAt = DateTime.UtcNow, ProcessedAt = processedAt });
            await db.SaveChangesAsync();
            var day = DateOnly.FromDateTime(processedAt.AddHours(7));
            var current = await reports.GetMembershipReportAsync(center.Id, today, today, "day");
            var later = await reports.GetMembershipReportAsync(center.Id, day, day, "day");
            return current.Packages!.Single(p => p.Id == package.Id).RevenueInPeriod == 1250000
                && later.Packages!.Single(p => p.Id == package.Id).RevenueInPeriod == -100000;
        });
        await Check("successful full cash refund updates invoice and subscription atomically", async () => {
            var subscription = new MemberSubscription { MemberId = member.Id, PackageId = package.Id,
                Price = 300000, DurationDays = 30, Status = "Active", StartDate = today, EndDate = today.AddDays(29), CreatedAt = DateTime.UtcNow };
            db.Add(subscription); await db.SaveChangesAsync();
            var invoice = new Invoice { InvoiceNumber = "SC-AUDIT-REFUND", MemberId = member.Id, CenterId = center.Id,
                CreatedBy = owner.Id, Subtotal = 300000, TotalAmount = 300000, Status = "Paid",
                IssuedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow };
            db.Add(invoice); await db.SaveChangesAsync();
            var payment = new Payment { InvoiceId = invoice.Id, MemberId = member.Id, PaymentMethod = "CASH",
                Amount = 300000, AmountReceived = 300000, PaymentStatus = "Succeeded", PaidAt = DateTime.UtcNow,
                AttemptedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            db.AddRange(new InvoiceItem { InvoiceId = invoice.Id, PackageId = package.Id, SubscriptionId = subscription.Id,
                Description = "Full refund fixture", Quantity = 1, UnitPrice = 300000, Amount = 300000 }, payment);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            var result = await new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build())
                .RefundPaymentAsync(manager.Id, center.Id, payment.Id, "full-cash-refund",
                    new CreateRefundRequest { Amount = 300000, Reason = "Approved full refund" }, "127.0.0.1");
            db.ChangeTracker.Clear();
            return result.Status == "Succeeded"
                && await db.Invoices.AnyAsync(i => i.Id == invoice.Id && i.Status == "Refunded")
                && await db.MemberSubscriptions.AnyAsync(s => s.Id == subscription.Id && s.Status == "Refunded")
                && await db.PaymentRefunds.CountAsync(r => r.PaymentId == payment.Id && r.Status == "Succeeded") == 1;
        });
        await Check("gateway refund reconciliation requires evidence and finalizes membership state", async () => {
            var subscription = new MemberSubscription { MemberId = member.Id, PackageId = package.Id,
                Price = 450000, DurationDays = 30, Status = "Active", StartDate = today, EndDate = today.AddDays(29), CreatedAt = DateTime.UtcNow };
            db.Add(subscription); await db.SaveChangesAsync();
            var invoice = new Invoice { InvoiceNumber = "SC-AUDIT-REFUND-RECONCILE", MemberId = member.Id, CenterId = center.Id,
                CreatedBy = owner.Id, Subtotal = 450000, TotalAmount = 450000, Status = "Paid",
                IssuedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow };
            db.Add(invoice); await db.SaveChangesAsync();
            var payment = new Payment { InvoiceId = invoice.Id, MemberId = member.Id, PaymentMethod = "VNPAY",
                Amount = 450000, AmountReceived = 450000, PaymentStatus = "Succeeded", GatewayReference = "AUDIT-GATEWAY-REFUND",
                ProviderTransactionId = "VNPAY:123456789", PaidAt = DateTime.UtcNow, AttemptedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
            db.Add(payment); await db.SaveChangesAsync();
            var refund = new PaymentRefund { PaymentId = payment.Id, Amount = 450000, Status = "Pending",
                IdempotencyKey = "audit-pending-refund", Reason = "Provider timeout requires reconciliation",
                RequestedBy = manager.Id, CreatedAt = DateTime.UtcNow };
            db.AddRange(new InvoiceItem { InvoiceId = invoice.Id, PackageId = package.Id, SubscriptionId = subscription.Id,
                Description = "Gateway refund fixture", Quantity = 1, UnitPrice = 450000, Amount = 450000 }, refund);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();

            var service = new PaymentService(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build());
            var missingReferenceRejected = false;
            try {
                await service.ReconcileRefundAsync(manager.Id, center.Id, refund.Id,
                    new ReconcileRefundRequest { Status = "Succeeded", EvidenceNote = "Verified provider record" });
            } catch (BusinessException) {
                missingReferenceRejected = true;
            }
            db.ChangeTracker.Clear();
            var result = await service.ReconcileRefundAsync(manager.Id, center.Id, refund.Id,
                new ReconcileRefundRequest { Status = "Succeeded", ProviderRefundId = "987654321", EvidenceNote = "Verified provider refund statement" });
            db.ChangeTracker.Clear();
            return missingReferenceRejected && result.Status == "Succeeded"
                && await db.Invoices.AnyAsync(i => i.Id == invoice.Id && i.Status == "Refunded")
                && await db.MemberSubscriptions.AnyAsync(s => s.Id == subscription.Id && s.Status == "Refunded")
                && await db.PaymentRefunds.AnyAsync(r => r.Id == refund.Id && r.ProviderRefundId == "VNPAY:987654321");
        });
        async Task<bool> ConcurrentAttempt(Func<CoachProfessionalService, Task<long>> action) {
            await using var isolated = new SportsCenterDbContext(options);
            try { return await action(new CoachProfessionalService(isolated, clock)) > 0; }
            catch (BusinessException e) when (e.StatusCode == HttpStatusCode.Conflict) { return false; }
            catch (Exception e) when (e.GetBaseException() is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.DeadlockDetected or Npgsql.PostgresErrorCodes.SerializationFailure }) { return false; }
        }
        await Check("two concurrent coach bookings commit at most one overlapping event", async () => {
            var request = new SaveWorkEventRequest(null, null, "Concurrent audit event", "Other",
                today.AddDays(4).ToDateTime(new TimeOnly(13, 0)), today.AddDays(4).ToDateTime(new TimeOnly(14, 0)), null);
            var results = await Task.WhenAll(ConcurrentAttempt(s => s.CreateWorkEvent(coach.Id, request, default)), ConcurrentAttempt(s => s.CreateWorkEvent(coach.Id, request, default)));
            return results.Count(r => r) == 1 && await db.CoachWorkEvents.CountAsync(e => e.Title == request.Title) == 1;
        });
        await Check("two concurrent leave requests commit only one overlapping request", async () => {
            var request = new SaveLeaveRequest(today.AddDays(5).ToDateTime(new TimeOnly(8, 0)), today.AddDays(6).ToDateTime(new TimeOnly(8, 0)), "AnnualLeave", "Concurrent audit leave", null);
            var results = await Task.WhenAll(ConcurrentAttempt(s => s.RequestLeave(coach.Id, request, default)), ConcurrentAttempt(s => s.RequestLeave(coach.Id, request, default)));
            return results.Count(r => r) == 1 && await db.CoachLeaves.CountAsync(l => l.Reason == request.Reason) == 1;
        });

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Jwt:Key"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), ["Jwt:Issuer"] = "logic-audit", ["Jwt:Audience"] = "logic-audit",
        });
        var issuer = new JwtTokenIssuer(builder.Configuration);
        builder.Services.AddSingleton(issuer);
        builder.Services.AddDbContext<SportsCenterDbContext>(o => o.UseNpgsql(auditConnection));
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        builder.Services.AddScoped<JwtAccountValidator>();
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<PasswordResetEmailSender>();
        var mailFixture = new RecordingMailService();
        builder.Services.AddSingleton<SportsCenterManagement.API.Services.ISendMailService>(mailFixture);
        var objectStorage = new RecordingObjectStorage();
        builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(objectStorage);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IBusinessClock, BusinessClock>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.Configure<SportsCenterManagement.API.Models.R2StorageOptions>(o => {
            o.BucketName = "audit-only"; o.PublicDomain = "https://media.example.test";
        });
        builder.Services.AddScoped<SportsCenterManagement.API.Services.IR2StorageService, SportsCenterManagement.API.Services.R2StorageService>();
        builder.Services.AddRateLimiter(o => {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            PasswordResetRateLimiting.Configure(o);
            MailSendRateLimiting.Configure(o);
        });
        builder.Services.AddScoped<IClassScheduleQueryService, ClassScheduleQueryService>();
        builder.Services.AddScoped<IPaymentService>(s => new PaymentService(s.GetRequiredService<IUnitOfWork>(), null!, null!, builder.Configuration));
        builder.Services.AddScoped<IMemberService, MemberService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IMemberActivationService, MemberActivationService>();
        builder.Services.AddSingleton<SportsCenterManagement.BLL.Common.MediaUrlBuilder>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<ICoreFlowService, CoreFlowService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
            o.MapInboundClaims = false; o.TokenValidationParameters = issuer.ValidationParameters();
            o.Events = new JwtBearerEvents {
                OnMessageReceived = BrowserSessionCookie.ReadAsync,
                OnTokenValidated = c => c.HttpContext.RequestServices.GetRequiredService<JwtAccountValidator>().ValidateAsync(c)
            };
        });
        builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddControllers().AddApplicationPart(typeof(PaymentsController).Assembly);
        await using var web = builder.Build();
        var mail = new FakeMailGateway();
        web.Use(async (context, next) => {
            // Test-only caller addresses allow independent checks of the real per-IP policy.
            if (IPAddress.TryParse(context.Request.Headers["X-Audit-Client"], out var address))
                context.Connection.RemoteIpAddress = address;
            try { await next(); }
            catch (BusinessException e) { context.Response.StatusCode = (int)e.StatusCode; await context.Response.WriteAsJsonAsync(new { message = e.Message }); }
            catch (Exception e) when (e.GetBaseException() is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.DeadlockDetected or Npgsql.PostgresErrorCodes.SerializationFailure }) {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        });
        web.UseRouting(); web.UseAuthentication(); web.UseAuthorization(); web.UseRateLimiter(); web.MapControllers();
        web.MapPost("/audit-email", mail.Receive).AllowAnonymous();
        await web.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(web.Urls.Single()) };
        apiClient = http;
        void Login(User account, string tokenRole = "Member", long? tokenCenter = null) => http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issuer.Issue(new(account.Id, account.Username, tokenRole, tokenCenter, account.Email)).Token);
        await Check("HTTP anonymous schedules rejected", async () => (await http.GetAsync("api/class-sessions")).StatusCode == HttpStatusCode.Unauthorized);
        await Check("HTTP production reset cannot claim an email was sent without a sender", async () => {
            var response = await http.PostAsJsonAsync("api/auth/request-password-reset", new { email = owner.Email });
            return response.StatusCode == HttpStatusCode.ServiceUnavailable && !await db.PasswordResetTokens.AnyAsync();
        });
        Login(owner, "Manager", other.Id);
        await Check("HTTP forged role and center claims cannot escape DB member scope", async () => {
            var response = await http.GetAsync($"api/class-sessions?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return response.IsSuccessStatusCode && body.GetProperty("totalCount").GetInt32() == 2
                && body.GetProperty("items").EnumerateArray().All(s => s.GetProperty("classId").GetInt64() == cls.Id);
        });
        await Check("HTTP schedule contract contains actual room name", async () => {
            var body = await http.GetFromJsonAsync<JsonElement>($"api/class-sessions?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            return body.GetProperty("items")[0].GetProperty("roomName").GetString() == room.Name;
        });
        await Check("HTTP invalid schedule dates return 400", async () => (await http.GetAsync("api/class-sessions?from=invalid")).StatusCode == HttpStatusCode.BadRequest);
        await Check("HTTP invalid pagination returns 400", async () => (await http.GetAsync("api/class-sessions?pageSize=0")).StatusCode == HttpStatusCode.BadRequest);
        await Check("HTTP forged manager role cannot modify class", async () => (await http.PostAsJsonAsync("api/classes", new { centerId = center.Id })).StatusCode == HttpStatusCode.Forbidden);
        await Check("HTTP subscription response exposes effectiveStatus", async () => {
            var response = await http.GetAsync($"api/members/{member.Id}/subscriptions");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return response.IsSuccessStatusCode && body.EnumerateArray().Any(s => s.GetProperty("effectiveStatus").GetString() == "SCHEDULED");
        });
        Login(coach, "Coach");
        await Check("HTTP coach cannot cancel another invoice", async () => (await http.DeleteAsync($"api/payments/pending-invoice/{pending.InvoiceNumber}")).StatusCode == HttpStatusCode.Forbidden);
        Login(staff, "Receptionist", center.Id);
        await Check("HTTP revoked cashier cannot cancel by POST", async () => (await http.PostAsJsonAsync("api/payments/cancel-pending", new { invoiceNumber = pending.InvoiceNumber })).StatusCode == HttpStatusCode.Forbidden);
        await Check("HTTP staff cannot pick arbitrary pending member by package", async () => (await http.DeleteAsync($"api/payments/pending-package/{package.Id}")).StatusCode == HttpStatusCode.BadRequest);
        builder.Configuration["ExternalCommunications:Email:WebhookUrl"] = web.Urls.Single() + "/audit-email";
        builder.Configuration["ExternalCommunications:Email:BearerToken"] = "audit-only-token";
        await RegistrationChecks.Run(db, http, mail, builder.Configuration, center.Id, Check);
        await AuthChecks.Run(db, http, issuer, owner, mail, Check);
        await ActivationChecks.Run(db, http, issuer, mail, builder.Configuration, Check);
        await OtpEmailChecks.Run(http, mail, builder.Configuration, Check);
        await VnPayCallbackChecks.Run(Check);
        await Check("manager coach-operations workspace returns resources, equipment issues and delivery data from the DB", async () => {
            var resource = new FacilityResource { CenterId = center.Id, Name = "Audit treadmill", ResourceType = "Equipment", Status = "Active", CreatedAt = DateTime.UtcNow };
            db.FacilityResources.Add(resource); await db.SaveChangesAsync();
            db.EquipmentIssues.Add(new EquipmentIssue { CenterId = center.Id, ResourceId = resource.Id, ReportedByUserId = manager.Id,
                Title = "Audit belt slipping", Description = "Belt slips at speed 8", Severity = "High", Status = "Open", ReportedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var workspace = await new CoachProfessionalService(db, clock).ManagerWorkspace(manager.Id, default);
            var json = JsonSerializer.Serialize(workspace, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;
            return root.TryGetProperty("deliverySummary", out _) && root.TryGetProperty("resourceBookings", out _)
                && root.TryGetProperty("salesCredits", out _)
                && root.GetProperty("resources").EnumerateArray().Any(r => r.GetProperty("name").GetString() == "Audit treadmill")
                && root.GetProperty("equipmentIssues").EnumerateArray().Any(i => i.GetProperty("title").GetString() == "Audit belt slipping"
                    && i.GetProperty("resourceName").GetString() == "Audit treadmill");
        });
        await RegistrationIdentityChecks.Run(db, http, mail, center.Id, Check);
        await CenterHoursChecks.Run(db, owner.Id, Check);
        await EnrollmentOptionsChecks.Run(db, Check);
        await PersistenceChecks.Run(db, http, issuer, objectStorage, builder.Configuration, Check);
        await BrowserSessionChecks.Run(db, http.BaseAddress!, mail, center.Id, Check);
        await MailSecurityChecks.Run(db, http, issuer, mailFixture, Check);
        await MediaSecurityChecks.Run(db, http, issuer, objectStorage, Check);
        await web.StopAsync();
    }
} finally {
    Npgsql.NpgsqlConnection.ClearAllPools();
    await using var command = master.CreateCommand();
    command.CommandText = $"DROP DATABASE \"{name}\" WITH (FORCE)";
    await command.ExecuteNonQueryAsync();
    await master.DisposeAsync();
    Console.WriteLine("Isolated audit database removed.");
}
Console.WriteLine($"RESULT {count - failures.Count}/{count} passed");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
    public override DateTimeOffset GetUtcNow() => now;
}

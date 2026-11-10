using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Context;

public sealed class SportsCenterDbContext(DbContextOptions<SportsCenterDbContext> options)
    : DbContext(options)
{
    static SportsCenterDbContext() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    public static string Reverse(string value) => throw new NotSupportedException("Chỉ dùng trong truy vấn LINQ được dịch sang SQL.");

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Center> Centers => Set<Center>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<MembershipPackage> MembershipPackages => Set<MembershipPackage>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<CoachProfile> CoachProfiles => Set<CoachProfile>();
    public DbSet<CoachSport> CoachSports => Set<CoachSport>();
    public DbSet<CoachLeave> CoachLeaves => Set<CoachLeave>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffCompensationPolicy> StaffCompensationPolicies => Set<StaffCompensationPolicy>();
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<AccountActivationToken> AccountActivationTokens => Set<AccountActivationToken>();
    public DbSet<MemberSubscription> MemberSubscriptions => Set<MemberSubscription>();
    public DbSet<ClassCoach> ClassCoaches => Set<ClassCoach>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<ClassWaitlist> ClassWaitlists => Set<ClassWaitlist>();
    public DbSet<CenterCheckin> CenterCheckins => Set<CenterCheckin>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<SessionBooking> SessionBookings => Set<SessionBooking>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<ClassEnrollment> ClassEnrollments => Set<ClassEnrollment>();
    public DbSet<TrainingPlanExercise> TrainingPlanExercises => Set<TrainingPlanExercise>();
    public DbSet<TrainingResult> TrainingResults => Set<TrainingResult>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<MemberProgressReview> MemberProgressReviews => Set<MemberProgressReview>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentRefund> PaymentRefunds => Set<PaymentRefund>();
    public DbSet<SupportRequestMessage> SupportRequestMessages => Set<SupportRequestMessage>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiExerciseRecommendation> AiExerciseRecommendations => Set<AiExerciseRecommendation>();
    public DbSet<UploadedMedia> UploadedMedias => Set<UploadedMedia>();
    public DbSet<CoachWorkEvent> CoachWorkEvents => Set<CoachWorkEvent>();
    public DbSet<MemberHealthRecord> MemberHealthRecords => Set<MemberHealthRecord>();
    public DbSet<NutritionPlan> NutritionPlans => Set<NutritionPlan>();
    public DbSet<FoodDiaryEntry> FoodDiaryEntries => Set<FoodDiaryEntry>();
    public DbSet<CoachMessage> CoachMessages => Set<CoachMessage>();
    public DbSet<CommunicationDelivery> CommunicationDeliveries => Set<CommunicationDelivery>();
    public DbSet<CoachCompensationPolicy> CoachCompensationPolicies => Set<CoachCompensationPolicy>();
    public DbSet<CoachSalesCredit> CoachSalesCredits => Set<CoachSalesCredit>();
    public DbSet<CoachReview> CoachReviews => Set<CoachReview>();
    public DbSet<FacilityResource> FacilityResources => Set<FacilityResource>();
    public DbSet<ResourceBooking> ResourceBookings => Set<ResourceBooking>();
    public DbSet<EquipmentIssue> EquipmentIssues => Set<EquipmentIssue>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // citext keeps SQL Server's case-insensitive comparisons (login, email, search, unique indexes).
        configurationBuilder.Properties<string>().HaveColumnType("citext");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("citext");
        // Storage keys must match exactly (previously Latin1_General_100_BIN2).
        modelBuilder.Entity<UploadedMedia>().Property(entity => entity.DriveFileId).HasColumnType("text");
        modelBuilder.Entity<PaymentRefund>().Property(entity => entity.TransferProofUrl).HasColumnType("text");

        modelBuilder.HasDbFunction(typeof(SportsCenterDbContext).GetMethod(nameof(Reverse), new[] { typeof(string) })!)
            .HasName("REVERSE")
            .IsBuiltIn();

        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.Entity<UploadedMedia>().HasIndex(entity => new { entity.CenterId, entity.CreatedAt });
        modelBuilder.Entity<UploadedMedia>().HasIndex(entity => entity.DriveFileId);
        modelBuilder.Entity<User>().HasIndex(entity => entity.GoogleSubject).IsUnique()
            .HasFilter("\"google_subject\" IS NOT NULL");
        modelBuilder.Entity<TrainingResult>().HasOne<ClassEntity>().WithMany()
            .HasForeignKey(entity => entity.ClassId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RolePermission>().HasKey(entity => new { entity.RoleId, entity.PermissionId });
        modelBuilder.Entity<ClassCoach>().HasKey(entity => new { entity.ClassId, entity.CoachId });
        modelBuilder.Entity<CoachSport>().HasKey(entity => new { entity.CoachId, entity.SportId });
        modelBuilder.Entity<StaffCompensationPolicy>().HasIndex(entity => new { entity.UserId, entity.Status });

        modelBuilder.Entity<RolePermission>()
            .HasOne<Role>()
            .WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RolePermission>()
            .HasOne<Permission>()
            .WithMany()
            .HasForeignKey(entity => entity.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ClassCoach>()
            .HasOne<ClassEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ClassCoach>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachLeave>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachSport>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<StaffCompensationPolicy>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StaffCompensationPolicy>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StaffCompensationPolicy>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachSport>()
            .HasOne<Sport>()
            .WithMany()
            .HasForeignKey(entity => entity.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Role>().HasIndex(entity => entity.Name).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<User>().HasIndex(entity => entity.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(entity => entity.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(entity => entity.Phone).IsUnique().HasFilter("\"phone\" IS NOT NULL");
        modelBuilder.Entity<MemberProfile>().HasIndex(entity => entity.UserId).IsUnique();
        modelBuilder.Entity<MemberProfile>().HasIndex(entity => entity.MemberCode).IsUnique();
        modelBuilder.Entity<MembershipPackage>().HasIndex(entity => new { entity.CenterId, entity.Name }).IsUnique();
        modelBuilder.Entity<CoachProfile>().HasIndex(entity => entity.UserId).IsUnique();
        modelBuilder.Entity<CoachProfile>().HasIndex(entity => entity.CoachCode).IsUnique();
        modelBuilder.Entity<StaffProfile>().HasIndex(entity => entity.UserId).IsUnique();
        modelBuilder.Entity<StaffProfile>().HasIndex(entity => entity.StaffCode).IsUnique();
        modelBuilder.Entity<PasswordResetToken>().HasIndex(entity => entity.Token).IsUnique();
        modelBuilder.Entity<EmailVerificationToken>().HasIndex(entity => new { entity.Email, entity.CreatedAt });
        modelBuilder.Entity<Invoice>().HasIndex(entity => entity.InvoiceNumber).IsUnique();
        modelBuilder.Entity<AuditLog>().HasIndex(entity => new { entity.CenterId, entity.CreatedAt });
        modelBuilder.Entity<Notification>().HasIndex(entity => entity.ReminderKey).IsUnique()
            .HasFilter("\"reminder_key\" IS NOT NULL");
        modelBuilder.Entity<SessionBooking>()
            .HasIndex(entity => new { entity.SessionId, entity.MemberId })
            .IsUnique();
        modelBuilder.Entity<SessionBooking>()
            .HasIndex(entity => new { entity.SubscriptionId, entity.Status, entity.SessionId });
        modelBuilder.Entity<ClassEnrollment>()
            .HasIndex(entity => new { entity.ClassId, entity.MemberId })
            .IsUnique();
        modelBuilder.Entity<ClassWaitlist>()
            .HasIndex(entity => new { entity.SessionId, entity.MemberId })
            .IsUnique()
            .HasFilter("\"session_id\" IS NOT NULL AND \"status\" = 'Waiting'");
        modelBuilder.Entity<ClassEntity>()
            .Property(entity => entity.AllowWaitlist)
            .HasDefaultValue(true);
        modelBuilder.Entity<Payment>()
            .HasIndex(entity => entity.TransactionCode)
            .IsUnique()
            .HasFilter("\"transaction_code\" IS NOT NULL");
        modelBuilder.Entity<Payment>().HasIndex(entity => entity.GatewayReference).IsUnique()
            .HasFilter("\"gateway_reference\" IS NOT NULL");
        modelBuilder.Entity<Payment>().HasIndex(entity => entity.ProviderTransactionId).IsUnique()
            .HasFilter("\"provider_transaction_id\" IS NOT NULL");
        modelBuilder.Entity<Payment>().HasIndex(entity => entity.IdempotencyKey).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        modelBuilder.Entity<Invoice>().HasIndex(entity => entity.IdempotencyKey).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        modelBuilder.Entity<PaymentRefund>().HasIndex(entity => entity.IdempotencyKey).IsUnique();
        modelBuilder.Entity<PaymentRefund>().HasIndex(entity => entity.ProviderRefundId).IsUnique()
            .HasFilter("\"provider_refund_id\" IS NOT NULL");
        modelBuilder.Entity<SystemSetting>().HasIndex(entity => entity.SettingKey).IsUnique();
        modelBuilder.Entity<CoachWorkEvent>().HasIndex(entity => new { entity.CoachId, entity.StartAt, entity.EndAt });
        modelBuilder.Entity<MemberHealthRecord>().HasIndex(entity => new { entity.MemberId, entity.OccurredOn });
        modelBuilder.Entity<NutritionPlan>().HasIndex(entity => new { entity.MemberId, entity.Status });
        modelBuilder.Entity<FoodDiaryEntry>().HasIndex(entity => new { entity.MemberId, entity.EntryDate });
        modelBuilder.Entity<CoachMessage>().HasIndex(entity => new { entity.CoachId, entity.MemberId, entity.CreatedAt });
        modelBuilder.Entity<CommunicationDelivery>().HasIndex(entity => new { entity.Status, entity.CreatedAt });
        modelBuilder.Entity<CommunicationDelivery>().HasIndex(entity => new { entity.Status, entity.NextAttemptAt, entity.LeaseUntil });
        modelBuilder.Entity<CoachCompensationPolicy>().HasIndex(entity => new { entity.CoachId, entity.EffectiveFrom });
        modelBuilder.Entity<CoachReview>().HasIndex(entity => new { entity.CoachId, entity.MemberId, entity.WorkEventId })
            .IsUnique().HasFilter("\"work_event_id\" IS NOT NULL");
        modelBuilder.Entity<FacilityResource>().HasIndex(entity => new { entity.CenterId, entity.Name }).IsUnique();
        modelBuilder.Entity<ResourceBooking>().HasIndex(entity => new { entity.ResourceId, entity.StartAt, entity.EndAt });
        modelBuilder.Entity<EquipmentIssue>().HasIndex(entity => new { entity.CenterId, entity.Status, entity.ReportedAt });

        modelBuilder.Entity<User>()
            .HasOne<Role>()
            .WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Room>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Sport>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MembershipPackage>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Exercise>()
            .HasOne<Sport>()
            .WithMany()
            .HasForeignKey(entity => entity.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Exercise>().HasOne<Center>().WithMany()
            .HasForeignKey(entity => entity.CenterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Exercise>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CreatedByCoachId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemberProfile>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<MemberProfile>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemberProfile>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CoachProfile>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<CoachProfile>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CoachProfile>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StaffProfile>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<StaffProfile>(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StaffProfile>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassEntity>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassEntity>()
            .HasOne<Sport>()
            .WithMany()
            .HasForeignKey(entity => entity.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassEntity>()
            .HasOne<Room>()
            .WithMany()
            .HasForeignKey(entity => entity.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SystemSetting>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AccountActivationToken>().HasIndex(entity => entity.TokenHash).IsUnique();
        modelBuilder.Entity<AccountActivationToken>().HasIndex(entity => new { entity.UserId, entity.CreatedAt });
        modelBuilder.Entity<AccountActivationToken>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemberSubscription>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemberSubscription>()
            .HasOne<MembershipPackage>()
            .WithMany()
            .HasForeignKey(entity => entity.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassSchedule>()
            .HasOne<ClassEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassSchedule>()
            .HasOne<Room>()
            .WithMany()
            .HasForeignKey(entity => entity.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassWaitlist>()
            .HasOne<ClassEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassWaitlist>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassWaitlist>()
            .HasOne<ClassSession>()
            .WithMany()
            .HasForeignKey(entity => entity.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassWaitlist>()
            .HasOne<MemberSubscription>()
            .WithMany()
            .HasForeignKey(entity => entity.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassWaitlist>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RegisteredBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CenterCheckin>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CenterCheckin>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CenterCheckin>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CheckedInBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingPlan>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingPlan>()
            .HasOne<ClassEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingPlan>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
            .HasOne<ClassEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Assignment>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserNotification>()
            .HasOne<Notification>()
            .WithMany()
            .HasForeignKey(entity => entity.NotificationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserNotification>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupportRequest>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupportRequest>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.HandledBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupportRequest>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupportRequest>()
            .HasOne<Center>()
            .WithMany()
            .HasForeignKey(entity => entity.CenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupportRequest>().HasIndex(entity => new { entity.RequesterId, entity.CreatedAt });
        modelBuilder.Entity<SupportRequest>().HasIndex(entity => new { entity.CenterId, entity.Status, entity.CreatedAt });

        modelBuilder.Entity<AiConversation>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiConversation>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassSession>()
            .HasOne<ClassEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassSession>()
            .HasOne<ClassSchedule>()
            .WithMany()
            .HasForeignKey(entity => entity.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassSession>()
            .HasOne<Room>()
            .WithMany()
            .HasForeignKey(entity => entity.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassSession>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SessionBooking>()
            .HasOne<ClassSession>()
            .WithMany()
            .HasForeignKey(entity => entity.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SessionBooking>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SessionBooking>()
            .HasOne<ClassEnrollment>()
            .WithMany()
            .HasForeignKey(entity => entity.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SessionBooking>()
            .HasOne<MemberSubscription>()
            .WithMany()
            .HasForeignKey(entity => entity.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SessionBooking>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RegisteredBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne<ClassSession>()
            .WithMany()
            .HasForeignKey(entity => entity.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attendance>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CheckedInBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassEnrollment>()
            .HasOne<ClassEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassEnrollment>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassEnrollment>()
            .HasOne<MemberSubscription>()
            .WithMany()
            .HasForeignKey(entity => entity.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClassEnrollment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RegisteredBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingPlanExercise>()
            .HasOne<TrainingPlan>()
            .WithMany()
            .HasForeignKey(entity => entity.TrainingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingPlanExercise>()
            .HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(entity => entity.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingResult>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingResult>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingResult>()
            .HasOne<ClassSession>()
            .WithMany()
            .HasForeignKey(entity => entity.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingResult>()
            .HasOne<TrainingPlanExercise>()
            .WithMany()
            .HasForeignKey(entity => entity.TrainingPlanExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingResult>()
            .HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(entity => entity.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingResult>()
            .HasOne<TrainingPlan>()
            .WithMany()
            .HasForeignKey(entity => entity.TrainingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(entity => entity.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne<MembershipPackage>()
            .WithMany()
            .HasForeignKey(entity => entity.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne<MemberSubscription>()
            .WithMany()
            .HasForeignKey(entity => entity.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne<ClassEnrollment>()
            .WithMany()
            .HasForeignKey(entity => entity.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemberProgressReview>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemberProgressReview>()
            .HasOne<CoachProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MemberProgressReview>()
            .HasOne<TrainingPlan>()
            .WithMany()
            .HasForeignKey(entity => entity.TrainingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AssignmentSubmission>()
            .HasOne<Assignment>()
            .WithMany()
            .HasForeignKey(entity => entity.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AssignmentSubmission>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(entity => entity.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ProcessedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RefundApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PaymentRefund>()
            .HasOne<Payment>()
            .WithMany()
            .HasForeignKey(entity => entity.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentRefund>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PaymentRefund>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupportRequestMessage>()
            .HasOne<SupportRequest>()
            .WithMany()
            .HasForeignKey(entity => entity.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupportRequestMessage>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiMessage>()
            .HasOne<AiConversation>()
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiExerciseRecommendation>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(entity => entity.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiExerciseRecommendation>()
            .HasOne<AiConversation>()
            .WithMany()
            .HasForeignKey(entity => entity.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiExerciseRecommendation>()
            .HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(entity => entity.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiExerciseRecommendation>()
            .HasOne<TrainingPlan>()
            .WithMany()
            .HasForeignKey(entity => entity.TrainingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AiExerciseRecommendation>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CoachWorkEvent>().HasOne<Center>().WithMany()
            .HasForeignKey(entity => entity.CenterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachWorkEvent>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachWorkEvent>().HasOne<MemberProfile>().WithMany()
            .HasForeignKey(entity => entity.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachWorkEvent>().HasOne<Room>().WithMany()
            .HasForeignKey(entity => entity.RoomId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachWorkEvent>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MemberHealthRecord>().HasOne<MemberProfile>().WithMany()
            .HasForeignKey(entity => entity.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MemberHealthRecord>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<NutritionPlan>().HasOne<MemberProfile>().WithMany()
            .HasForeignKey(entity => entity.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<NutritionPlan>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<FoodDiaryEntry>().HasOne<MemberProfile>().WithMany()
            .HasForeignKey(entity => entity.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachMessage>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachMessage>().HasOne<MemberProfile>().WithMany()
            .HasForeignKey(entity => entity.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachMessage>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CommunicationDelivery>().HasOne<Notification>().WithMany()
            .HasForeignKey(entity => entity.NotificationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CommunicationDelivery>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachCompensationPolicy>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachCompensationPolicy>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachSalesCredit>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachSalesCredit>().HasOne<MemberProfile>().WithMany()
            .HasForeignKey(entity => entity.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachSalesCredit>().HasOne<MemberSubscription>().WithMany()
            .HasForeignKey(entity => entity.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachSalesCredit>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachReview>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachReview>().HasOne<MemberProfile>().WithMany()
            .HasForeignKey(entity => entity.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachReview>().HasOne<CoachWorkEvent>().WithMany()
            .HasForeignKey(entity => entity.WorkEventId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<FacilityResource>().HasOne<Center>().WithMany()
            .HasForeignKey(entity => entity.CenterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResourceBooking>().HasOne<FacilityResource>().WithMany()
            .HasForeignKey(entity => entity.ResourceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResourceBooking>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.CoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ResourceBooking>().HasOne<CoachWorkEvent>().WithMany()
            .HasForeignKey(entity => entity.WorkEventId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentIssue>().HasOne<Center>().WithMany()
            .HasForeignKey(entity => entity.CenterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentIssue>().HasOne<FacilityResource>().WithMany()
            .HasForeignKey(entity => entity.ResourceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentIssue>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EquipmentIssue>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachLeave>().HasOne<CoachProfile>().WithMany()
            .HasForeignKey(entity => entity.SwapCoachId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CoachLeave>().HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

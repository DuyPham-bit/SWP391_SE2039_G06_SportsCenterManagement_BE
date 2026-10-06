using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Context;

public sealed class SportsCenterDbContext(DbContextOptions<SportsCenterDbContext> options)
    : DbContext(options)
{
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
    public DbSet<CoachLeave> CoachLeaves => Set<CoachLeave>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
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
    public DbSet<SupportRequestMessage> SupportRequestMessages => Set<SupportRequestMessage>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiExerciseRecommendation> AiExerciseRecommendations => Set<AiExerciseRecommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.Entity<RolePermission>().HasKey(entity => new { entity.RoleId, entity.PermissionId });
        modelBuilder.Entity<ClassCoach>().HasKey(entity => new { entity.ClassId, entity.CoachId });

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

        modelBuilder.Entity<Role>().HasIndex(entity => entity.Name).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<User>().HasIndex(entity => entity.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(entity => entity.Email).IsUnique();
        modelBuilder.Entity<MemberProfile>().HasIndex(entity => entity.UserId).IsUnique();
        modelBuilder.Entity<MemberProfile>().HasIndex(entity => entity.MemberCode).IsUnique();
        modelBuilder.Entity<CoachProfile>().HasIndex(entity => entity.UserId).IsUnique();
        modelBuilder.Entity<CoachProfile>().HasIndex(entity => entity.CoachCode).IsUnique();
        modelBuilder.Entity<StaffProfile>().HasIndex(entity => entity.UserId).IsUnique();
        modelBuilder.Entity<StaffProfile>().HasIndex(entity => entity.StaffCode).IsUnique();
        modelBuilder.Entity<PasswordResetToken>().HasIndex(entity => entity.Token).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(entity => entity.InvoiceNumber).IsUnique();
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
            .HasFilter("[session_id] IS NOT NULL AND [status] = N'Waiting'");
        modelBuilder.Entity<ClassEntity>()
            .Property(entity => entity.AllowWaitlist)
            .HasDefaultValue(true);
        modelBuilder.Entity<Payment>()
            .HasIndex(entity => entity.TransactionCode)
            .IsUnique()
            .HasFilter("[transaction_code] IS NOT NULL");
        modelBuilder.Entity<SystemSetting>().HasIndex(entity => entity.SettingKey).IsUnique();

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

        modelBuilder.Entity<MemberProfile>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<MemberProfile>(entity => entity.UserId)
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

        modelBuilder.Entity<SystemSetting>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UpdatedBy)
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
    }
}

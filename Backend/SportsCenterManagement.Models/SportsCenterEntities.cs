using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.Models;

[Table("roles")]
public class Role : Common.BaseEntity
{
    [MaxLength(50)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("permissions")]
public class Permission : Common.BaseEntity
{
    [MaxLength(100)]
    [Column("code")]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

}

[Table("role_permissions")]
public class RolePermission
{
    [Column("role_id")]
    public long RoleId { get; set; }

    [Column("permission_id")]
    public long PermissionId { get; set; }

}

[Table("users")]
public class User : Common.BaseEntity
{
    [MaxLength(100)]
    [Column("username")]
    public string Username { get; set; } = null!;

    [MaxLength(150)]
    [Column("email")]
    public string Email { get; set; } = null!;

    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [Column("role_id")]
    public long RoleId { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("failed_login_attempts")]
    public int FailedLoginAttempts { get; set; }

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("centers")]
public class Center : Common.BaseEntity
{
    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    [Column("address")]
    public string Address { get; set; } = null!;

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(150)]
    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("opening_time")]
    public TimeOnly? OpeningTime { get; set; }

    [Column("closing_time")]
    public TimeOnly? ClosingTime { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("rooms")]
public class Room : Common.BaseEntity
{
    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(100)]
    [Column("room_type")]
    public string? RoomType { get; set; }

    [Column("capacity")]
    public int Capacity { get; set; }

    [MaxLength(255)]
    [Column("location_description")]
    public string? LocationDescription { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("sports")]
public class Sport : Common.BaseEntity
{
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}

[Table("membership_packages")]
public class MembershipPackage : Common.BaseEntity
{
    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("duration_days")]
    public int DurationDays { get; set; }

    [Precision(12, 2)]
    [Column("price")]
    public decimal Price { get; set; }

    [Column("max_classes")]
    public int? MaxClasses { get; set; }

    [MaxLength(50)]
    [Column("access_type")]
    public string? AccessType { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("exercises")]
public class Exercise : Common.BaseEntity
{
    [Column("sport_id")]
    public long? SportId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(100)]
    [Column("muscle_group")]
    public string? MuscleGroup { get; set; }

    [MaxLength(50)]
    [Column("difficulty_level")]
    public string? DifficultyLevel { get; set; }

    [Column("instructions")]
    public string? Instructions { get; set; }

    [MaxLength(500)]
    [Column("video_url")]
    public string? VideoUrl { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("member_profiles")]
public class MemberProfile : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [MaxLength(50)]
    [Column("member_code")]
    public string MemberCode { get; set; } = null!;

    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("date_of_birth")]
    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    [Column("gender")]
    public string? Gender { get; set; }

    [MaxLength(255)]
    [Column("address")]
    public string? Address { get; set; }

    [MaxLength(150)]
    [Column("emergency_contact_name")]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    [Column("emergency_contact_phone")]
    public string? EmergencyContactPhone { get; set; }

    [MaxLength(500)]
    [Column("fitness_goal")]
    public string? FitnessGoal { get; set; }

    [MaxLength(50)]
    [Column("fitness_level")]
    public string? FitnessLevel { get; set; }

    [MaxLength(1000)]
    [Column("medical_note")]
    public string? MedicalNote { get; set; }

    [Precision(5, 2)]
    [Column("height_cm")]
    public decimal? HeightCm { get; set; }

    [Precision(5, 2)]
    [Column("weight_kg")]
    public decimal? WeightKg { get; set; }

    [Column("booking_suspended_until")]
    public DateTime? BookingSuspendedUntil { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("coach_profiles")]
public class CoachProfile : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(50)]
    [Column("coach_code")]
    public string CoachCode { get; set; } = null!;

    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [MaxLength(255)]
    [Column("specialization")]
    public string? Specialization { get; set; }

    [MaxLength(500)]
    [Column("certification")]
    public string? Certification { get; set; }

    [Column("experience_years")]
    public int? ExperienceYears { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("hire_date")]
    public DateOnly? HireDate { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("staff_profiles")]
public class StaffProfile : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(50)]
    [Column("staff_code")]
    public string StaffCode { get; set; } = null!;

    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [MaxLength(100)]
    [Column("position")]
    public string Position { get; set; } = null!;

    [Column("hire_date")]
    public DateOnly? HireDate { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("classes")]
public class ClassEntity : Common.BaseEntity
{
    [Column("center_id")]
    public long CenterId { get; set; }

    [Column("sport_id")]
    public long SportId { get; set; }

    [Column("room_id")]
    public long? RoomId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(50)]
    [Column("level")]
    public string? Level { get; set; }

    [Column("capacity")]
    public int Capacity { get; set; }

    [Column("duration_minutes")]
    public int DurationMinutes { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("notifications")]
public class Notification : Common.BaseEntity
{
    [Column("sender_id")]
    public long SenderId { get; set; }

    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("message")]
    public string Message { get; set; } = null!;

    [MaxLength(50)]
    [Column("notification_type")]
    public string NotificationType { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

}

[Table("audit_logs")]
public class AuditLog : Common.BaseEntity
{
    [Column("user_id")]
    public long? UserId { get; set; }

    [MaxLength(100)]
    [Column("action")]
    public string Action { get; set; } = null!;

    [MaxLength(100)]
    [Column("entity_type")]
    public string EntityType { get; set; } = null!;

    [Column("entity_id")]
    public long? EntityId { get; set; }

    [Column("old_values")]
    public string? OldValues { get; set; }

    [Column("new_values")]
    public string? NewValues { get; set; }

    [MaxLength(45)]
    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("system_settings")]
public class SystemSetting : Common.BaseEntity
{
    [MaxLength(100)]
    [Column("setting_key")]
    public string SettingKey { get; set; } = null!;

    [Column("setting_value")]
    public string? SettingValue { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("updated_by")]
    public long? UpdatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("password_reset_tokens")]
public class PasswordResetToken : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [MaxLength(255)]
    [Column("token")]
    public string Token { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("member_subscriptions")]
public class MemberSubscription : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("package_id")]
    public long PackageId { get; set; }

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [Precision(12, 2)]
    [Column("price")]
    public decimal Price { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("auto_renew")]
    public bool AutoRenew { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("class_coaches")]
public class ClassCoach : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("assigned_date")]
    public DateOnly? AssignedDate { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

}

[Table("class_schedules")]
public class ClassSchedule : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("room_id")]
    public long? RoomId { get; set; }

    [Column("day_of_week")]
    public int DayOfWeek { get; set; }

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; }

    [Column("start_date")]
    public DateOnly? StartDate { get; set; }

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}

[Table("class_waitlist")]
public class ClassWaitlist : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}

[Table("center_checkins")]
public class CenterCheckin : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [Column("checked_in_by")]
    public long? CheckedInBy { get; set; }

    [Column("check_in_time")]
    public DateTime CheckInTime { get; set; }

    [Column("check_out_time")]
    public DateTime? CheckOutTime { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("training_plans")]
public class TrainingPlan : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(500)]
    [Column("goal")]
    public string? Goal { get; set; }

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    [MaxLength(30)]
    [Column("plan_type")]
    public string PlanType { get; set; } = null!;

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}

[Table("assignments")]
public class Assignment : Common.BaseEntity
{
    [Column("coach_id")]
    public long? CoachId { get; set; }

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Column("member_id")]
    public long? MemberId { get; set; }

    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}

[Table("invoices")]
public class Invoice : Common.BaseEntity
{
    [MaxLength(50)]
    [Column("invoice_number")]
    public string InvoiceNumber { get; set; } = null!;

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [Column("created_by")]
    public long? CreatedBy { get; set; }

    [Precision(12, 2)]
    [Column("subtotal")]
    public decimal Subtotal { get; set; }

    [Precision(12, 2)]
    [Column("discount")]
    public decimal Discount { get; set; }

    [Precision(12, 2)]
    [Column("tax")]
    public decimal Tax { get; set; }

    [Precision(12, 2)]
    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("issued_at")]
    public DateTime IssuedAt { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

}

[Table("user_notifications")]
public class UserNotification : Common.BaseEntity
{
    [Column("notification_id")]
    public long NotificationId { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("support_requests")]
public class SupportRequest : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("handled_by")]
    public long? HandledBy { get; set; }

    [MaxLength(200)]
    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [MaxLength(30)]
    [Column("priority")]
    public string Priority { get; set; } = null!;

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

}

[Table("ai_conversations")]
public class AiConversation : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("member_id")]
    public long? MemberId { get; set; }

    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = null!;

    [MaxLength(50)]
    [Column("conversation_type")]
    public string ConversationType { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

}

[Table("class_sessions")]
public class ClassSession : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("schedule_id")]
    public long? ScheduleId { get; set; }

    [Column("room_id")]
    public long? RoomId { get; set; }

    [Column("coach_id")]
    public long? CoachId { get; set; }

    [Column("session_date")]
    public DateOnly SessionDate { get; set; }

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; }

    [MaxLength(30)]
    [Column("session_status")]
    public string SessionStatus { get; set; } = null!;

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("session_bookings")]
public class SessionBooking : Common.BaseEntity
{
    [Column("session_id")]
    public long SessionId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("enrollment_id")]
    public long? EnrollmentId { get; set; }

    [Column("booked_at")]
    public DateTime BookedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}

[Table("attendance")]
public class Attendance : Common.BaseEntity
{
    [Column("session_id")]
    public long SessionId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("checked_in_by")]
    public long? CheckedInBy { get; set; }

    [MaxLength(30)]
    [Column("attendance_status")]
    public string AttendanceStatus { get; set; } = null!;

    [Column("check_in_time")]
    public DateTime? CheckInTime { get; set; }

    [Column("check_out_time")]
    public DateTime? CheckOutTime { get; set; }

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("class_enrollments")]
public class ClassEnrollment : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("subscription_id")]
    public long? SubscriptionId { get; set; }

    [Column("registered_by")]
    public long? RegisteredBy { get; set; }

    [Column("registered_at")]
    public DateTime RegisteredAt { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [MaxLength(500)]
    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

}

[Table("training_plan_exercises")]
public class TrainingPlanExercise : Common.BaseEntity
{
    [Column("training_plan_id")]
    public long TrainingPlanId { get; set; }

    [Column("exercise_id")]
    public long ExerciseId { get; set; }

    [Column("day_number")]
    public int? DayNumber { get; set; }

    [Column("sets")]
    public int? Sets { get; set; }

    [Column("repetitions")]
    public int? Repetitions { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Column("rest_seconds")]
    public int? RestSeconds { get; set; }

    [Precision(8, 2)]
    [Column("target_weight")]
    public decimal? TargetWeight { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }

}

[Table("training_results")]
public class TrainingResult : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("session_id")]
    public long? SessionId { get; set; }

    [Column("training_plan_exercise_id")]
    public long? TrainingPlanExerciseId { get; set; }

    [Column("exercise_id")]
    public long? ExerciseId { get; set; }

    [Column("training_plan_id")]
    public long? TrainingPlanId { get; set; }

    [Column("result_date")]
    public DateOnly ResultDate { get; set; }

    [Column("sets_completed")]
    public int? SetsCompleted { get; set; }

    [Column("repetitions_completed")]
    public int? RepetitionsCompleted { get; set; }

    [Precision(8, 2)]
    [Column("weight")]
    public decimal? Weight { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Precision(8, 2)]
    [Column("calories_burned")]
    public decimal? CaloriesBurned { get; set; }

    [Precision(5, 2)]
    [Column("performance_score")]
    public decimal? PerformanceScore { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("invoice_items")]
public class InvoiceItem : Common.BaseEntity
{
    [Column("invoice_id")]
    public long InvoiceId { get; set; }

    [Column("package_id")]
    public long? PackageId { get; set; }

    [Column("subscription_id")]
    public long? SubscriptionId { get; set; }

    [Column("enrollment_id")]
    public long? EnrollmentId { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Precision(12, 2)]
    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Precision(12, 2)]
    [Column("amount")]
    public decimal Amount { get; set; }

}

[Table("member_progress_reviews")]
public class MemberProgressReview : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("training_plan_id")]
    public long? TrainingPlanId { get; set; }

    [Column("review_date")]
    public DateOnly ReviewDate { get; set; }

    [Precision(5, 2)]
    [Column("progress_score")]
    public decimal? ProgressScore { get; set; }

    [Precision(8, 2)]
    [Column("weight")]
    public decimal? Weight { get; set; }

    [Precision(5, 2)]
    [Column("body_fat_percentage")]
    public decimal? BodyFatPercentage { get; set; }

    [MaxLength(2000)]
    [Column("review_note")]
    public string? ReviewNote { get; set; }

    [MaxLength(1000)]
    [Column("next_goal")]
    public string? NextGoal { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("assignment_submissions")]
public class AssignmentSubmission : Common.BaseEntity
{
    [Column("assignment_id")]
    public long AssignmentId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    [Precision(5, 2)]
    [Column("score")]
    public decimal? Score { get; set; }

    [MaxLength(1000)]
    [Column("coach_feedback")]
    public string? CoachFeedback { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}

[Table("payments")]
public class Payment : Common.BaseEntity
{
    [Column("invoice_id")]
    public long InvoiceId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("processed_by")]
    public long? ProcessedBy { get; set; }

    [MaxLength(50)]
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = null!;

    [MaxLength(150)]
    [Column("transaction_code")]
    public string? TransactionCode { get; set; }

    [Precision(12, 2)]
    [Column("amount")]
    public decimal Amount { get; set; }

    [MaxLength(30)]
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = null!;

    [Column("paid_at")]
    public DateTime PaidAt { get; set; }

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("refund_approved_by")]
    public long? RefundApprovedBy { get; set; }

}

[Table("support_request_messages")]
public class SupportRequestMessage : Common.BaseEntity
{
    [Column("request_id")]
    public long RequestId { get; set; }

    [Column("sender_id")]
    public long SenderId { get; set; }

    [Column("message")]
    public string Message { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("ai_messages")]
public class AiMessage : Common.BaseEntity
{
    [Column("conversation_id")]
    public long ConversationId { get; set; }

    [MaxLength(30)]
    [Column("sender_type")]
    public string SenderType { get; set; } = null!;

    [Column("message")]
    public string Message { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

[Table("ai_exercise_recommendations")]
public class AiExerciseRecommendation : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("conversation_id")]
    public long? ConversationId { get; set; }

    [Column("exercise_id")]
    public long? ExerciseId { get; set; }

    [Column("training_plan_id")]
    public long? TrainingPlanId { get; set; }

    [Column("requested_by")]
    public long? RequestedBy { get; set; }

    [Column("recommendation_reason")]
    public string? RecommendationReason { get; set; }

    [MaxLength(500)]
    [Column("target_goal")]
    public string? TargetGoal { get; set; }

    [MaxLength(50)]
    [Column("difficulty_level")]
    public string? DifficultyLevel { get; set; }

    [MaxLength(100)]
    [Column("ai_model")]
    public string? AiModel { get; set; }

    [Precision(5, 4)]
    [Column("confidence_score")]
    public decimal? ConfidenceScore { get; set; }

    [Column("accepted")]
    public bool? Accepted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}

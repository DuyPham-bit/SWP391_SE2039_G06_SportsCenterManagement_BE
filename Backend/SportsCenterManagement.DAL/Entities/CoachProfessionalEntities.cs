using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("coach_work_events")]
public sealed class CoachWorkEvent : BaseEntity
{
    [Column("center_id")] public long CenterId { get; set; }
    [Column("coach_id")] public long CoachId { get; set; }
    [Column("member_id")] public long? MemberId { get; set; }
    [Column("room_id")] public long? RoomId { get; set; }
    [MaxLength(200), Column("title")] public string Title { get; set; } = null!;
    [MaxLength(30), Column("event_type")] public string EventType { get; set; } = null!;
    [Column("start_at")] public DateTime StartAt { get; set; }
    [Column("end_at")] public DateTime EndAt { get; set; }
    [MaxLength(30), Column("status")] public string Status { get; set; } = null!;
    [MaxLength(1000), Column("notes")] public string? Notes { get; set; }
    [Column("created_by")] public long CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime? UpdatedAt { get; set; }
}

[Table("member_health_records")]
public sealed class MemberHealthRecord : BaseEntity
{
    [Column("member_id")] public long MemberId { get; set; }
    [Column("coach_id")] public long CoachId { get; set; }
    [MaxLength(40), Column("record_type")] public string RecordType { get; set; } = null!;
    [MaxLength(200), Column("title")] public string Title { get; set; } = null!;
    [Column("details")] public string Details { get; set; } = null!;
    [Column("occurred_on")] public DateOnly OccurredOn { get; set; }
    [MaxLength(30), Column("status")] public string Status { get; set; } = "Active";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("nutrition_plans")]
public sealed class NutritionPlan : BaseEntity
{
    [Column("member_id")] public long MemberId { get; set; }
    [Column("coach_id")] public long CoachId { get; set; }
    [MaxLength(200), Column("title")] public string Title { get; set; } = null!;
    [Column("start_date")] public DateOnly StartDate { get; set; }
    [Column("end_date")] public DateOnly? EndDate { get; set; }
    [Column("daily_calories")] public int DailyCalories { get; set; }
    [Column("protein_grams")] public int ProteinGrams { get; set; }
    [Column("carb_grams")] public int CarbGrams { get; set; }
    [Column("fat_grams")] public int FatGrams { get; set; }
    [Column("meal_plan_json")] public string? MealPlanJson { get; set; }
    [MaxLength(1000), Column("notes")] public string? Notes { get; set; }
    [MaxLength(30), Column("status")] public string Status { get; set; } = "Active";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime? UpdatedAt { get; set; }
}

[Table("food_diary_entries")]
public sealed class FoodDiaryEntry : BaseEntity
{
    [Column("member_id")] public long MemberId { get; set; }
    [Column("entry_date")] public DateOnly EntryDate { get; set; }
    [MaxLength(40), Column("meal_type")] public string MealType { get; set; } = null!;
    [MaxLength(300), Column("food_name")] public string FoodName { get; set; } = null!;
    [Column("calories")] public int? Calories { get; set; }
    [Precision(8, 2), Column("protein_grams")] public decimal? ProteinGrams { get; set; }
    [Precision(8, 2), Column("carb_grams")] public decimal? CarbGrams { get; set; }
    [Precision(8, 2), Column("fat_grams")] public decimal? FatGrams { get; set; }
    [MaxLength(1000), Column("notes")] public string? Notes { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("coach_messages")]
public sealed class CoachMessage : BaseEntity
{
    [Column("coach_id")] public long CoachId { get; set; }
    [Column("member_id")] public long MemberId { get; set; }
    [Column("sender_user_id")] public long SenderUserId { get; set; }
    [MaxLength(4000), Column("content")] public string Content { get; set; } = null!;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("read_at")] public DateTime? ReadAt { get; set; }
}

[Table("communication_deliveries")]
public sealed class CommunicationDelivery : BaseEntity
{
    [Column("notification_id")] public long? NotificationId { get; set; }
    [Column("recipient_user_id")] public long RecipientUserId { get; set; }
    [MaxLength(20), Column("channel")] public string Channel { get; set; } = null!;
    [MaxLength(30), Column("status")] public string Status { get; set; } = null!;
    [MaxLength(1000), Column("provider_message")] public string? ProviderMessage { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("sent_at")] public DateTime? SentAt { get; set; }
}

[Table("coach_compensation_policies")]
public sealed class CoachCompensationPolicy : BaseEntity
{
    [Column("coach_id")] public long CoachId { get; set; }
    [Precision(14, 2), Column("monthly_base_salary")] public decimal MonthlyBaseSalary { get; set; }
    [Precision(14, 2), Column("commission_per_session")] public decimal CommissionPerSession { get; set; }
    [Precision(5, 2), Column("sales_commission_percent")] public decimal SalesCommissionPercent { get; set; }
    [Column("target_sessions")] public int TargetSessions { get; set; }
    [Precision(14, 2), Column("target_sales")] public decimal TargetSales { get; set; }
    [Column("effective_from")] public DateOnly EffectiveFrom { get; set; }
    [Column("effective_to")] public DateOnly? EffectiveTo { get; set; }
    [MaxLength(30), Column("status")] public string Status { get; set; } = "Active";
    [Column("updated_by")] public long UpdatedBy { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("coach_sales_credits")]
public sealed class CoachSalesCredit : BaseEntity
{
    [Column("coach_id")] public long CoachId { get; set; }
    [Column("member_id")] public long? MemberId { get; set; }
    [Column("subscription_id")] public long? SubscriptionId { get; set; }
    [Precision(14, 2), Column("amount")] public decimal Amount { get; set; }
    [Column("credited_at")] public DateTime CreditedAt { get; set; }
    [MaxLength(500), Column("note")] public string? Note { get; set; }
    [Column("created_by")] public long CreatedBy { get; set; }
}

[Table("coach_reviews")]
public sealed class CoachReview : BaseEntity
{
    [Column("coach_id")] public long CoachId { get; set; }
    [Column("member_id")] public long MemberId { get; set; }
    [Column("work_event_id")] public long? WorkEventId { get; set; }
    [Column("rating")] public int Rating { get; set; }
    [MaxLength(2000), Column("comment")] public string? Comment { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime? UpdatedAt { get; set; }
}

[Table("facility_resources")]
public sealed class FacilityResource : BaseEntity
{
    [Column("center_id")] public long CenterId { get; set; }
    [MaxLength(150), Column("name")] public string Name { get; set; } = null!;
    [MaxLength(50), Column("resource_type")] public string ResourceType { get; set; } = null!;
    [MaxLength(300), Column("location")] public string? Location { get; set; }
    [Column("capacity")] public int? Capacity { get; set; }
    [MaxLength(30), Column("status")] public string Status { get; set; } = "Available";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("resource_bookings")]
public sealed class ResourceBooking : BaseEntity
{
    [Column("resource_id")] public long ResourceId { get; set; }
    [Column("coach_id")] public long CoachId { get; set; }
    [Column("work_event_id")] public long? WorkEventId { get; set; }
    [Column("start_at")] public DateTime StartAt { get; set; }
    [Column("end_at")] public DateTime EndAt { get; set; }
    [MaxLength(500), Column("purpose")] public string Purpose { get; set; } = null!;
    [MaxLength(30), Column("status")] public string Status { get; set; } = "Confirmed";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("equipment_issues")]
public sealed class EquipmentIssue : BaseEntity
{
    [Column("center_id")] public long CenterId { get; set; }
    [Column("resource_id")] public long? ResourceId { get; set; }
    [Column("reported_by_user_id")] public long ReportedByUserId { get; set; }
    [MaxLength(200), Column("title")] public string Title { get; set; } = null!;
    [Column("description")] public string Description { get; set; } = null!;
    [MaxLength(20), Column("severity")] public string Severity { get; set; } = null!;
    [MaxLength(1000), Column("photo_url")] public string? PhotoUrl { get; set; }
    [MaxLength(30), Column("status")] public string Status { get; set; } = "Open";
    [MaxLength(1000), Column("resolution_note")] public string? ResolutionNote { get; set; }
    [Column("resolved_by")] public long? ResolvedBy { get; set; }
    [Column("reported_at")] public DateTime ReportedAt { get; set; }
    [Column("resolved_at")] public DateTime? ResolvedAt { get; set; }
}

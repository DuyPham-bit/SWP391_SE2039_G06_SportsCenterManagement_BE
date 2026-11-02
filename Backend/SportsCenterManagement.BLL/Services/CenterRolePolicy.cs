using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.BLL.Services;

public sealed record CenterRoleOverride(string[] PermissionCodes, string[] CapabilityIds);
public sealed record CenterUserOverride(string[] PermissionCodes, string[] CapabilityIds);

public static class CenterRolePolicy
{
    public static string Key(long centerId, long roleId) => $"center.rbac.{centerId}.{roleId}";
    public static string UserKey(long centerId, long userId) => $"center.rbac.user.{centerId}.{userId}";

    // Managers can delegate within each role's business boundary, never grant administrative roles.
    public static string[] Capabilities(string role) => role.ToUpperInvariant() switch
    {
        "MEMBER" => ["book_class", "cancel_booking", "view_my_progress", "ask_ai_assistant", "use_coach_portal"],
        "COACH" => ["view_teaching_schedule", "take_attendance", "create_training_plan", "get_ai_recommendation", "manage_coach_workspace"],
        "RECEPTIONIST" => ["lookup_member", "register_counter_package", "checkin_member"],
        "MANAGER" => ["lookup_member", "register_counter_package", "manage_staff", "manage_packages", "manage_classes", "assign_coach", "view_reports", "view_audit_logs", "manage_coach_operations"],
        "ADMIN" => ["manage_centers", "manage_managers", "manage_accounts", "manage_roles", "view_system_audit", "manage_landing_content"],
        _ => []
    };

    public static async Task<long?> CenterForUser(SportsCenterDbContext db, long userId, CancellationToken ct)
    {
        var staffCenter = await db.StaffProfiles.Where(s => s.UserId == userId && s.Status == "Active")
            .Select(s => (long?)s.CenterId).SingleOrDefaultAsync(ct);
        if (staffCenter.HasValue) return staffCenter;
        var coachCenter = await db.CoachProfiles.Where(c => c.UserId == userId && c.Status == "Active")
            .Select(c => (long?)c.CenterId).SingleOrDefaultAsync(ct);
        return coachCenter ?? await db.MemberProfiles.Where(m => m.UserId == userId).Select(m => m.CenterId).SingleOrDefaultAsync(ct);
    }

    public static async Task<CenterRoleOverride?> Read(SportsCenterDbContext db, long centerId, long roleId, CancellationToken ct)
    {
        var key = Key(centerId, roleId);
        var value = await db.SystemSettings.Where(s => s.SettingKey == key).Select(s => s.SettingValue).SingleOrDefaultAsync(ct);
        return value is null ? null : JsonSerializer.Deserialize<CenterRoleOverride>(value);
    }

    public static async Task<CenterUserOverride?> ReadUser(SportsCenterDbContext db, long centerId, long userId, CancellationToken ct)
    {
        var value = await db.SystemSettings.Where(s => s.SettingKey == UserKey(centerId, userId))
            .Select(s => s.SettingValue).SingleOrDefaultAsync(ct);
        return value is null ? null : JsonSerializer.Deserialize<CenterUserOverride>(value);
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

public sealed record LandingContentResponse(JsonElement? Content, DateTime? UpdatedAt);

public sealed class LandingContentService(SportsCenterDbContext db, IConfiguration configuration)
{
    private const string SettingKey = "site.landing-content";
    // Unpublished editor work, shared by every Manager/Admin and kept across devices.
    private const string DraftKey = "site.landing-content.draft";
    private static readonly string[] RequiredSections = ["heroBanner", "whyUs", "coursesSection", "facilitiesSection", "zonesLayout", "ctaAndFooter"];
    private static readonly string[] Sections = ["heroBanner", "whyUs", "coursesSection", "facilitiesSection", "zonesLayout", "ctaAndFooter", "authShowcase"];
    private static readonly HashSet<string> ObjectArrays = ["metrics", "features", "stats", "sportsList", "facilitiesList", "zones", "serviceAmenities", "specs", "trustBadges"];
    private static readonly HashSet<string> TextArrays = ["tags", "badges", "amenities", "items"];
    private static readonly HashSet<string> Booleans = ["isVisible", "isAutoCount", "formatThousands"];

    public async Task<LandingContentResponse> GetAsync(CancellationToken ct)
    {
        var setting = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s => s.SettingKey == SettingKey, ct);
        if (setting?.SettingValue is null) return new(null, null);
        using var document = JsonDocument.Parse(setting.SettingValue);
        return new(TransformR2Urls(document.RootElement, toPublicUrl: true), setting.UpdatedAt);
    }

    public async Task<LandingContentResponse> SaveAsync(JsonElement content, long userId, CancellationToken ct)
    {
        var storedContent = TransformR2Urls(content, toPublicUrl: false);
        Validate(storedContent);
        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == SettingKey, ct);
        var previous = new { setting?.UpdatedAt, setting?.UpdatedBy };
        if (setting is null)
        {
            setting = new SystemSetting { SettingKey = SettingKey, Description = "Nội dung trang chủ công khai" };
            db.SystemSettings.Add(setting);
        }
        setting.SettingValue = storedContent.GetRawText();
        setting.UpdatedBy = userId;
        setting.UpdatedAt = DateTime.UtcNow;
        await SyncCenterPhoneAsync(storedContent, userId, ct);
        AddAudit(userId, "landing.content.published", previous, new { setting.UpdatedAt, Bytes = setting.SettingValue.Length });
        await db.SaveChangesAsync(ct);
        // Publishing consumes the draft — only once the published content is safely stored.
        await db.SystemSettings.Where(s => s.SettingKey == DraftKey).ExecuteDeleteAsync(ct);
        return new(TransformR2Urls(storedContent, toPublicUrl: true), setting.UpdatedAt);
    }

    public async Task<LandingContentResponse> GetDraftAsync(CancellationToken ct)
    {
        var draft = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s => s.SettingKey == DraftKey, ct);
        if (draft?.SettingValue is null) return new(null, null);
        using var document = JsonDocument.Parse(draft.SettingValue);
        return new(TransformR2Urls(document.RootElement, toPublicUrl: true), draft.UpdatedAt);
    }

    /// <summary>Autosaved, possibly incomplete content: only shape-checked, never shown publicly.</summary>
    /// <param name="expectedUpdatedAt">
    /// The draft version the editor last loaded/saved. When given and another editor has saved since,
    /// the write is rejected (409) instead of silently overwriting their work. Omitted = legacy last-write-wins.
    /// </param>
    public async Task<LandingContentResponse> SaveDraftAsync(JsonElement content, long userId, CancellationToken ct,
        DateTime? expectedUpdatedAt = null)
    {
        if (content.ValueKind != JsonValueKind.Object)
            throw new FlowException(400, "Bản nháp trang chủ phải là một đối tượng JSON.");
        var stored = TransformR2Urls(content, toPublicUrl: false);
        var draft = await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == DraftKey, ct);
        // Postgres keeps microseconds while the echoed value may carry ticks, so compare with a 1 ms tolerance.
        if (expectedUpdatedAt is { } expected && draft?.UpdatedAt is { } current
            && Math.Abs((current.ToUniversalTime() - expected.ToUniversalTime()).TotalMilliseconds) >= 1)
            throw new FlowException(409, "Bản nháp trang chủ vừa được người khác chỉnh sửa. Tải lại trang để xem bản mới nhất trước khi tiếp tục.");
        if (draft is null)
        {
            draft = new SystemSetting { SettingKey = DraftKey, Description = "Bản nháp nội dung trang chủ (chưa xuất bản)" };
            db.SystemSettings.Add(draft);
        }
        draft.SettingValue = stored.GetRawText();
        draft.UpdatedBy = userId;
        draft.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(TransformR2Urls(stored, toPublicUrl: true), draft.UpdatedAt);
    }

    public async Task DiscardDraftAsync(long userId, CancellationToken ct)
    {
        var draft = await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == DraftKey, ct);
        if (draft is null) return;
        // Delete and audit commit together.
        db.SystemSettings.Remove(draft);
        AddAudit(userId, "landing.draft.discarded", new { draft.UpdatedAt, draft.UpdatedBy }, null);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Keeps Center.Phone aligned with the published footer hotline for the publisher's center (or sole active center).</summary>
    internal async Task SyncCenterPhoneAsync(JsonElement content, long userId, CancellationToken ct)
    {
        if (!content.TryGetProperty("ctaAndFooter", out var footer)
            || !footer.TryGetProperty("contactPhone", out var phoneProp)
            || phoneProp.ValueKind != JsonValueKind.String)
            return;

        var phone = phoneProp.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(phone) || phone.Length > 20) return;

        var actorCenterId = await db.StaffProfiles.AsNoTracking()
            .Where(staff => staff.UserId == userId && staff.Status == "Active")
            .Select(staff => (long?)staff.CenterId)
            .FirstOrDefaultAsync(ct);

        Center? center = null;
        if (actorCenterId.HasValue)
        {
            center = await db.Centers.SingleOrDefaultAsync(item => item.Id == actorCenterId.Value, ct);
        }
        else
        {
            var activeCenters = await db.Centers.Where(item => item.Status == "Active").Take(2).ToListAsync(ct);
            if (activeCenters.Count == 1) center = activeCenters[0];
        }

        if (center is not null && !string.Equals(center.Phone, phone, StringComparison.Ordinal))
        {
            center.Phone = phone;
        }
    }

    private void AddAudit(long userId, string action, object? oldValues, object? newValues) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = "SystemSetting",
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CreatedAt = DateTime.UtcNow
        });

    private JsonElement TransformR2Urls(JsonElement content, bool toPublicUrl)
    {
        var publicDomain = configuration["R2Storage:PublicDomain"]?.Trim().TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrEmpty(publicDomain))
        {
            return content.Clone();
        }
        var node = JsonNode.Parse(content.GetRawText())!;
        TransformNode(node, publicDomain, toPublicUrl);
        return JsonSerializer.SerializeToElement(node);
    }

    private static void TransformNode(JsonNode node, string publicDomain, bool toPublicUrl)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    if (toPublicUrl && text.StartsWith("dashboard/", StringComparison.Ordinal))
                        obj[property.Key] = $"{publicDomain}/{text}";
                    else if (!toPublicUrl && text.StartsWith($"{publicDomain}/dashboard/", StringComparison.Ordinal))
                        obj[property.Key] = text[(publicDomain.Length + 1)..];
                }
                else if (property.Value is not null) TransformNode(property.Value, publicDomain, toPublicUrl);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                if (child is not null) TransformNode(child, publicDomain, toPublicUrl);
        }
    }

    internal static void Validate(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(content.GetRawText()) > 1024 * 1024)
            throw InvalidContent();
        foreach (var section in RequiredSections)
            if (!content.TryGetProperty(section, out var value) || value.ValueKind != JsonValueKind.Object) throw InvalidContent();
        foreach (var section in content.EnumerateObject())
        {
            if (!Sections.Contains(section.Name)) throw InvalidContent();
            ValidateObject(section.Value, 0);
        }
    }

    private static void ValidateObject(JsonElement value, int depth)
    {
        if (depth > 8) throw InvalidContent();
        foreach (var property in value.EnumerateObject())
        {
            var item = property.Value;
            if (ObjectArrays.Contains(property.Name) || TextArrays.Contains(property.Name))
            {
                if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() > 200) throw InvalidContent();
                var ids = new HashSet<string>();
                foreach (var entry in item.EnumerateArray())
                {
                    if (TextArrays.Contains(property.Name)) { ValidateText(entry, property.Name); continue; }
                    if (entry.ValueKind != JsonValueKind.Object) throw InvalidContent();
                    ValidateObject(entry, depth + 1);
                    if (property.Name is "sportsList" or "facilitiesList")
                    {
                        if (!entry.TryGetProperty("id", out var id) || string.IsNullOrWhiteSpace(id.GetString()) || !ids.Add(id.GetString()!) ||
                            !entry.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString())) throw InvalidContent();
                    }
                }
            }
            else if (property.Name == "trustBanner")
            {
                if (item.ValueKind != JsonValueKind.Object) throw InvalidContent();
                ValidateObject(item, depth + 1);
            }
            else if (Booleans.Contains(property.Name))
            {
                if (item.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidContent();
            }
            else if (property.Name == "number")
            {
                if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number) || !double.IsFinite(number) || number < 0 || number > 1_000_000_000) throw InvalidContent();
            }
            else ValidateText(item, property.Name);
        }
    }

    private static void ValidateText(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString()!.Length > 10000) throw InvalidContent();
        if (name.EndsWith("Url", StringComparison.Ordinal) || name.EndsWith("Link", StringComparison.Ordinal) || name is "image" or "secondaryImage")
        {
            var url = value.GetString()!.Trim();
            if (url.Length == 0) return;
            if (url.Any(char.IsControl) || url.Contains('\\') || url.StartsWith("//")) throw InvalidContent();
            if (url.StartsWith('#') || url.StartsWith('/') || url.StartsWith("./") ||
                url.StartsWith("dashboard/", StringComparison.Ordinal)) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw InvalidContent();
        }
    }

    private static FlowException InvalidContent() => new(400, "Nội dung trang chủ không hợp lệ. Kiểm tra văn bản, danh sách và liên kết HTTP/HTTPS.");
}

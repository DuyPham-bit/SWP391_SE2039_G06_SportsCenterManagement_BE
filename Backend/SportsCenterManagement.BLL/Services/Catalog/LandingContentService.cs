using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Seed;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services.Catalog;

public sealed record LandingContentResponse(JsonElement? Content, DateTime? UpdatedAt);
public sealed record VisibilityCount(int Visible, int Total);
public sealed record HomeContentStats(VisibilityCount Sports, VisibilityCount Facilities);
public sealed record PublicHomeContentResponse(JsonElement? Content, HomeContentStats Stats, string Lang, DateTime? UpdatedAt);
/// <param name="MissingTranslations">Paths whose Vietnamese text has no English yet (a warning, not an error).</param>
public sealed record AdminHomeContentResponse(JsonElement? Content, HomeContentStats Stats, DateTime? UpdatedAt, IReadOnlyList<string> MissingTranslations);

/// <summary>
/// Home page content stored as one bilingual JSON document in SystemSettings. Sport/room visibility and the
/// "x/y visible" counts live on the Sports/Rooms tables; cards whose id is a DB id follow those columns.
/// </summary>
public sealed class LandingContentService(IUnitOfWork unitOfWork, IConfiguration configuration)
{
    // Data access goes through the unit of work (same scoped DbContext instance).
    private readonly SportsCenterDbContext db = unitOfWork.Context;

    private const string SettingKey = "site.landing-content";
    // Unpublished editor work, shared by every Manager/Admin and kept across devices.
    private const string DraftKey = "site.landing-content.draft";
    private const string SeedResource = "SportsCenterManagement.BLL.Seed.home-content.seed.json";
    private static readonly Lazy<(JsonObject Content, Dictionary<string, JsonObject> LegacyText)> Seed = new(LoadSeed);
    private static readonly Lazy<IReadOnlyDictionary<string, JsonObject>> SeedTranslations = new(() => BuildTranslations(new JsonObject()));

    // ---------- New API (/api/home-content, /api/admin/home-content) ----------

    public async Task<PublicHomeContentResponse> GetPublicAsync(string? lang, long? centerId, CancellationToken ct)
    {
        var language = HomeContentDocument.NormalizeLanguage(lang);
        var (setting, doc) = await LoadAsync(SettingKey, ct);
        var stats = await GetStatsAsync(await ResolveCenterIdAsync(centerId, null, ct), doc, ct);
        if (doc is null) return new(null, stats, language, null);
        await ApplyVisibilityAsync(doc, ct);
        var localized = HomeContentDocument.Localize(doc, language);
        return new(ToPublicElement(localized), stats, language, setting!.UpdatedAt);
    }

    public async Task<AdminHomeContentResponse> GetAdminAsync(long? centerId, long userId, CancellationToken ct)
    {
        var (setting, doc) = await LoadAsync(SettingKey, ct);
        var stats = await GetStatsAsync(await ResolveCenterIdAsync(centerId, userId, ct), doc, ct);
        if (doc is null) return new(null, stats, null, []);
        await ApplyVisibilityAsync(doc, ct);
        return new(ToPublicElement(doc), stats, setting!.UpdatedAt, MissingTranslations(doc));
    }

    /// <summary>Publishes the whole document ("Lưu toàn bộ thay đổi").</summary>
    public async Task<AdminHomeContentResponse> PublishAsync(JsonElement content, long userId, long? centerId, CancellationToken ct)
    {
        var doc = JsonNode.Parse(content.GetRawText()) as JsonObject
                  ?? throw new FlowException(400, "Nội dung trang chủ phải là một đối tượng JSON.");
        await SaveDocumentAsync(doc, userId, "all", ct);
        return await GetAdminAsync(centerId, userId, ct);
    }

    /// <summary>Publishes one section; the rest of the published document is kept.</summary>
    public async Task<AdminHomeContentResponse> SaveSectionAsync(string section, JsonElement content, long userId, long? centerId, CancellationToken ct)
    {
        if (!HomeContentDocument.Sections.Contains(section))
            throw new FlowException(404, $"Khu vực \"{section}\" không tồn tại.");
        if (JsonNode.Parse(content.GetRawText()) is not JsonObject sectionContent)
            throw new FlowException(400, "Nội dung khu vực phải là một đối tượng JSON.");
        var (_, doc) = await LoadAsync(SettingKey, ct);
        doc ??= (JsonObject)Seed.Value.Content.DeepClone();
        doc[section] = sectionContent;
        await SaveDocumentAsync(doc, userId, section, ct);
        return await GetAdminAsync(centerId, userId, ct);
    }

    /// <summary>The shared editor draft, in its bilingual form.</summary>
    public async Task<LandingContentResponse> GetAdminDraftAsync(CancellationToken ct)
    {
        var (draft, doc) = await LoadAsync(DraftKey, ct);
        return doc is null ? new(null, null) : new(ToPublicElement(doc), draft!.UpdatedAt);
    }

    // ---------- Legacy API (/api/landing-content): single language (vi), kept until the FE moves over ----------

    public async Task<LandingContentResponse> GetAsync(CancellationToken ct)
    {
        var (setting, doc) = await LoadAsync(SettingKey, ct);
        if (doc is null) return new(null, null);
        await ApplyVisibilityAsync(doc, ct);
        return new(ToPublicElement(HomeContentDocument.Localize(doc, HomeContentDocument.DefaultLanguage)), setting!.UpdatedAt);
    }

    /// <summary>Accepts flat (Vietnamese) or bilingual content; English already known for a text is kept.</summary>
    public async Task<LandingContentResponse> SaveAsync(JsonElement content, long userId, CancellationToken ct)
    {
        var doc = JsonNode.Parse(content.GetRawText()) as JsonObject
                  ?? throw new FlowException(400, "Nội dung trang chủ phải là một đối tượng JSON.");
        await SaveDocumentAsync(doc, userId, "all", ct);
        return await GetAsync(ct);
    }

    public async Task<LandingContentResponse> GetDraftAsync(CancellationToken ct)
    {
        var (draft, doc) = await LoadAsync(DraftKey, ct);
        return doc is null ? new(null, null)
            : new(ToPublicElement(HomeContentDocument.Localize(doc, HomeContentDocument.DefaultLanguage)), draft!.UpdatedAt);
    }

    // ---------- Draft (shared by both APIs) ----------

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
        var stored = TransformR2Urls(JsonNode.Parse(content.GetRawText())!, toPublicUrl: false);
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
        draft.SettingValue = stored.ToJsonString();
        draft.UpdatedBy = userId;
        draft.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(ToPublicElement(stored), draft.UpdatedAt);
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

    /// <summary>Validation entry point kept for callers that hold a JsonElement. Returns missing-English paths.</summary>
    internal static IReadOnlyList<string> Validate(JsonElement content) =>
        HomeContentDocument.Validate(JsonNode.Parse(content.GetRawText()));

    // ---------- Seeding ----------

    /// <summary>
    /// Seeds the bilingual home content when none exists, or upgrades a legacy single-language document:
    /// text becomes {vi,en} (English filled from the seed where the Vietnamese matches), fixed counts become
    /// {{sportCount}}/{{facilityCount}} placeholders and card ids are re-pointed to the Sports/Rooms rows.
    /// </summary>
    public static async Task EnsureSeededAsync(SportsCenterDbContext db, CancellationToken ct = default)
    {
        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == SettingKey, ct);
        JsonObject doc;
        if (setting?.SettingValue is null)
        {
            doc = (JsonObject)Seed.Value.Content.DeepClone();
        }
        else
        {
            if (JsonNode.Parse(setting.SettingValue) is not JsonObject stored) return;
            if (stored["schemaVersion"] is JsonValue version && version.TryGetValue<int>(out var v) && v >= HomeContentDocument.SchemaVersion) return;
            doc = stored;
            HomeContentDocument.Normalize(doc, BuildTranslations(null));
            BindCountsToDatabase(doc);
        }
        await MapCardsToCatalogAsync(db, doc, ct);
        doc["schemaVersion"] = HomeContentDocument.SchemaVersion;
        if (setting is null)
        {
            setting = new SystemSetting { SettingKey = SettingKey };
            db.SystemSettings.Add(setting);
        }
        setting.Description = "Nội dung trang chủ công khai (song ngữ vi/en)";
        setting.SettingValue = doc.ToJsonString();
        setting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The auto-counted statistics read their numbers from the database instead of typed values.</summary>
    internal static void BindCountsToDatabase(JsonObject doc)
    {
        if (doc["whyUs"]?["stats"] is JsonArray stats)
            foreach (var stat in stats.OfType<JsonObject>())
                if (Str(stat["id"]) is "s1" or "s2") stat["isAutoCount"] = true;
        if (doc["authShowcase"] is JsonObject auth)
        {
            if (IsWholeNumber(auth["stat1Value"])) auth["stat1Value"] = "{{sportCount}}";
            if (IsWholeNumber(auth["stat2Value"])) auth["stat2Value"] = "{{facilityCount}}";
        }
    }

    private static bool IsWholeNumber(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 && s.All(char.IsAsciiDigit);

    /// <summary>
    /// Points legacy slug ids ("golf", "room-01") at Sport/Room ids. Sports match by name (word order ignored);
    /// rooms by name, else through the sport card that links to them and the seeded sport→room pairing.
    /// Cards with no matching record keep their id and stay editorial-only.
    /// </summary>
    internal static async Task MapCardsToCatalogAsync(SportsCenterDbContext db, JsonObject doc, CancellationToken ct)
    {
        var sportCards = (doc["coursesSection"]?["sportsList"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var roomCards = (doc["facilitiesSection"]?["facilitiesList"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        if (sportCards.Count == 0 && roomCards.Count == 0) return;

        var sports = await db.Sports.AsNoTracking().Where(s => s.Status == "Active").OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.Status == "Active").OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.Name }).ToListAsync(ct);
        var sportByKey = sports.GroupBy(s => NameKey(s.Name)).ToDictionary(g => g.Key, g => g.First());
        var roomByKey = rooms.GroupBy(r => NameKey(r.Name)).ToDictionary(g => g.Key, g => g.First());
        var seededRoomBySport = DbInitializer.SportsCatalog.ToDictionary(c => NameKey(c.SportName), c => NameKey(c.RoomName));

        var usedSports = new HashSet<string>(sportCards.Select(c => CardId(c)).Where(IsDbId)!);
        var usedRooms = new HashSet<string>(roomCards.Select(c => CardId(c)).Where(IsDbId)!);
        var roomIdMap = new Dictionary<string, string>();
        var sportKeyByLegacyRoom = new Dictionary<string, string>();

        foreach (var card in sportCards)
        {
            var id = CardId(card);
            if (id is null || IsDbId(id) || !sportByKey.TryGetValue(NameKey(CardName(card)), out var sport)) continue;
            var newId = sport.Id.ToString();
            if (!usedSports.Add(newId)) continue;
            card["id"] = newId;
            if (Str(card["facilityId"]) is { Length: > 0 } legacyRoom)
                sportKeyByLegacyRoom.TryAdd(legacyRoom, NameKey(sport.Name));
        }
        foreach (var card in roomCards)
        {
            var id = CardId(card);
            if (id is null || IsDbId(id)) continue;
            var room = roomByKey.GetValueOrDefault(NameKey(CardName(card)));
            if (room is null && sportKeyByLegacyRoom.TryGetValue(id, out var sportKey)
                && seededRoomBySport.TryGetValue(sportKey, out var roomKey))
                room = roomByKey.GetValueOrDefault(roomKey);
            if (room is null) continue;
            var newId = room.Id.ToString();
            if (!usedRooms.Add(newId)) continue;
            card["id"] = newId;
            roomIdMap[id] = newId;
        }
        foreach (var card in sportCards)
            if (Str(card["facilityId"]) is { } link && roomIdMap.TryGetValue(link, out var mapped))
                card["facilityId"] = mapped;
    }

    private static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string? CardId(JsonObject card) => Str(card["id"]);

    private static string CardName(JsonObject card) =>
        HomeContentDocument.IsLocalized(card["name"]) ? HomeContentDocument.Text(card["name"], "vi")
        : Str(card["name"]) ?? string.Empty;

    private static bool IsDbId(string? id) => long.TryParse(id, out var n) && n > 0;

    /// <summary>Case- and order-insensitive word key: "Võ thuật & Boxing" == "Boxing & Võ thuật".</summary>
    internal static string NameKey(string name) =>
        string.Join(' ', new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal));

    // ---------- Internals ----------

    private async Task SaveDocumentAsync(JsonObject doc, long userId, string scope, CancellationToken ct)
    {
        var (setting, stored) = await LoadAsync(SettingKey, ct, tracking: true);
        TransformR2Urls(doc, toPublicUrl: false);
        HomeContentDocument.Normalize(doc, BuildTranslations(stored));
        doc["schemaVersion"] = HomeContentDocument.SchemaVersion;
        HomeContentDocument.Validate(doc);

        var previous = new { setting?.UpdatedAt, setting?.UpdatedBy };
        if (setting is null)
        {
            setting = new SystemSetting { SettingKey = SettingKey, Description = "Nội dung trang chủ công khai (song ngữ vi/en)" };
            db.SystemSettings.Add(setting);
        }
        setting.SettingValue = doc.ToJsonString();
        setting.UpdatedBy = userId;
        setting.UpdatedAt = DateTime.UtcNow;
        await SyncVisibilityAsync(doc, ct);
        await SyncCenterPhoneAsync(JsonSerializer.SerializeToElement(doc), userId, ct);
        AddAudit(userId, "landing.content.published", previous, new { setting.UpdatedAt, Section = scope, Bytes = setting.SettingValue.Length });
        await db.SaveChangesAsync(ct);
        // Publishing everything consumes the draft — only once the published content is safely stored.
        if (scope == "all" && await db.SystemSettings.SingleOrDefaultAsync(s => s.SettingKey == DraftKey, ct) is { } draft)
        {
            db.SystemSettings.Remove(draft);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Seed translations, overridden by those already in the stored document, plus legacy count texts.</summary>
    private static IReadOnlyDictionary<string, JsonObject> BuildTranslations(JsonNode? stored)
    {
        if (stored is null) return SeedTranslations.Value;
        var translations = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        HomeContentDocument.CollectTranslations(Seed.Value.Content, translations);
        HomeContentDocument.CollectTranslations(stored, translations);
        foreach (var (legacy, localized) in Seed.Value.LegacyText) translations[legacy] = localized;
        return translations;
    }

    private async Task<(SystemSetting? Setting, JsonObject? Document)> LoadAsync(string key, CancellationToken ct, bool tracking = false)
    {
        var query = tracking ? db.SystemSettings : db.SystemSettings.AsNoTracking();
        var setting = await query.SingleOrDefaultAsync(s => s.SettingKey == key, ct);
        if (setting?.SettingValue is null || JsonNode.Parse(setting.SettingValue) is not JsonObject doc) return (setting, null);
        // Older or flat documents are read in their bilingual form.
        HomeContentDocument.Normalize(doc, BuildTranslations(null));
        return (setting, doc);
    }

    private async Task ApplyVisibilityAsync(JsonObject doc, CancellationToken ct)
    {
        var sportCards = DbCards(doc["coursesSection"]?["sportsList"]);
        var roomCards = DbCards(doc["facilitiesSection"]?["facilitiesList"]);
        if (sportCards.Count > 0)
        {
            var ids = sportCards.Keys.ToList();
            var visible = await db.Sports.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.IsVisibleOnLanding, ct);
            foreach (var (id, card) in sportCards) if (visible.TryGetValue(id, out var v)) card["isVisible"] = v;
        }
        if (roomCards.Count > 0)
        {
            var ids = roomCards.Keys.ToList();
            var visible = await db.Rooms.AsNoTracking().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.IsVisibleOnLanding, ct);
            foreach (var (id, card) in roomCards) if (visible.TryGetValue(id, out var v)) card["isVisible"] = v;
        }
    }

    private async Task SyncVisibilityAsync(JsonObject doc, CancellationToken ct)
    {
        var sportCards = DbCards(doc["coursesSection"]?["sportsList"]);
        var roomCards = DbCards(doc["facilitiesSection"]?["facilitiesList"]);
        if (sportCards.Count > 0)
        {
            var ids = sportCards.Keys.ToList();
            foreach (var sport in await db.Sports.Where(s => ids.Contains(s.Id)).ToListAsync(ct))
                sport.IsVisibleOnLanding = IsCardVisible(sportCards[sport.Id]);
        }
        if (roomCards.Count > 0)
        {
            var ids = roomCards.Keys.ToList();
            foreach (var room in await db.Rooms.Where(r => ids.Contains(r.Id)).ToListAsync(ct))
                room.IsVisibleOnLanding = IsCardVisible(roomCards[room.Id]);
        }
    }

    private static bool IsCardVisible(JsonObject card) =>
        card["isVisible"] is not JsonValue v || !v.TryGetValue<bool>(out var visible) || visible;

    private static Dictionary<long, JsonObject> DbCards(JsonNode? list)
    {
        var cards = new Dictionary<long, JsonObject>();
        if (list is not JsonArray array) return cards;
        foreach (var card in array.OfType<JsonObject>())
            if (long.TryParse(CardId(card), out var id) && id > 0) cards.TryAdd(id, card);
        return cards;
    }

    /// <summary>
    /// Counts from the Sports/Rooms tables. When the home page has cards linked to DB records, only those
    /// records count (one card = one sport / one facility cluster), so "x/y" matches the cards shown;
    /// courts that are not on the home page (extra rooms of a cluster) are not counted. Without linked
    /// cards (catalog fallback) every active record counts.
    /// </summary>
    public async Task<HomeContentStats> GetStatsAsync(long? centerId, JsonObject? doc, CancellationToken ct)
    {
        var sports = db.Sports.AsNoTracking().Where(s => s.Status == "Active");
        var rooms = db.Rooms.AsNoTracking().Where(r => r.Status == "Active");
        if (centerId is { } id)
        {
            sports = sports.Where(s => s.CenterId == id || s.CenterId == null);
            rooms = rooms.Where(r => r.CenterId == id);
        }
        var sportIds = DbCards(doc?["coursesSection"]?["sportsList"]).Keys.ToList();
        var roomIds = DbCards(doc?["facilitiesSection"]?["facilitiesList"]).Keys.ToList();
        if (sportIds.Count > 0) sports = sports.Where(s => sportIds.Contains(s.Id));
        if (roomIds.Count > 0) rooms = rooms.Where(r => roomIds.Contains(r.Id));
        return new(
            new(await sports.CountAsync(s => s.IsVisibleOnLanding, ct), await sports.CountAsync(ct)),
            new(await rooms.CountAsync(r => r.IsVisibleOnLanding, ct), await rooms.CountAsync(ct)));
    }

    /// <summary>Requested center, else the staff member's center, else the only active center; null counts every center.</summary>
    private async Task<long?> ResolveCenterIdAsync(long? centerId, long? userId, CancellationToken ct)
    {
        if (centerId is > 0) return centerId;
        if (userId is { } uid)
        {
            var staffCenter = await db.StaffProfiles.AsNoTracking()
                .Where(staff => staff.UserId == uid && staff.Status == "Active")
                .Select(staff => (long?)staff.CenterId).FirstOrDefaultAsync(ct);
            if (staffCenter.HasValue) return staffCenter;
        }
        var active = await db.Centers.AsNoTracking().Where(c => c.Status == "Active").Select(c => c.Id).Take(2).ToListAsync(ct);
        return active.Count == 1 ? active[0] : null;
    }

    private static IReadOnlyList<string> MissingTranslations(JsonObject doc)
    {
        try { return HomeContentDocument.Validate(doc); }
        catch (FlowException) { return []; } // stored content predates a rule; the editor reports it on save
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

    private JsonElement ToPublicElement(JsonNode node) =>
        JsonSerializer.SerializeToElement(TransformR2Urls(node.DeepClone(), toPublicUrl: true));

    /// <summary>R2 objects are stored as "dashboard/..." keys and served with the configured public domain.</summary>
    private JsonNode TransformR2Urls(JsonNode node, bool toPublicUrl)
    {
        var publicDomain = configuration["R2Storage:PublicDomain"]?.Trim().TrimEnd('/') ?? string.Empty;
        if (!string.IsNullOrEmpty(publicDomain)) TransformNode(node, publicDomain, toPublicUrl);
        return node;
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

    private static (JsonObject Content, Dictionary<string, JsonObject> LegacyText) LoadSeed()
    {
        using var stream = typeof(LandingContentService).Assembly.GetManifestResourceStream(SeedResource)
                           ?? throw new InvalidOperationException($"Missing embedded resource {SeedResource}.");
        var root = JsonNode.Parse(stream)!.AsObject();
        var legacy = root["legacyText"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.AsObject(), StringComparer.Ordinal);
        return (root["content"]!.AsObject(), legacy);
    }
}

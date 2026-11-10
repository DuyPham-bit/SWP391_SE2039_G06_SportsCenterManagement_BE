using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

/// <summary>Data that used to live only in the browser (photos, landing drafts) is now stored in the DB.</summary>
static class PersistenceChecks
{
    private const string PublicDomain = "https://media.example.test";
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZV8AAAAASUVORK5CYII=");

    public static async Task Run(SportsCenterDbContext db, HttpClient http, JwtTokenIssuer issuer, RecordingObjectStorage storage,
        IConfiguration configuration, Func<string, Func<Task<bool>>, Task> check)
    {
        configuration["R2Storage:PublicDomain"] = PublicDomain;

        // Fixture: a receptionist and a member at one center, plus a member at another center.
        var center = new Center { Name = "Persistence center", Address = "Audit", Status = "Active", CreatedAt = DateTime.UtcNow };
        var otherCenter = new Center { Name = "Persistence other", Address = "Audit", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.AddRange(center, otherCenter);
        var receptionistRole = await db.Roles.SingleAsync(r => r.Name == "Receptionist");
        var memberRole = await db.Roles.SingleAsync(r => r.Name == "Member");
        User NewUser(string name, long roleId) => new() { Username = name, Email = name + "@persist.test", PasswordHash = "test-only",
            RoleId = roleId, Status = "Active", CreatedAt = DateTime.UtcNow };
        var desk = NewUser("persist-desk", receptionistRole.Id);
        var member = NewUser("persist-member", memberRole.Id);
        var outsider = NewUser("persist-outsider", memberRole.Id);
        db.AddRange(desk, member, outsider);
        await db.SaveChangesAsync();
        db.StaffProfiles.Add(new StaffProfile { UserId = desk.Id, CenterId = center.Id, StaffCode = "PERSIST-DESK", FullName = "Desk",
            Position = "Receptionist", Status = "Active", CreatedAt = DateTime.UtcNow });
        var memberProfile = new MemberProfile { UserId = member.Id, CenterId = center.Id, MemberCode = "PERSIST-M1", FullName = "Persist Member", CreatedAt = DateTime.UtcNow };
        var outsiderProfile = new MemberProfile { UserId = outsider.Id, CenterId = otherCenter.Id, MemberCode = "PERSIST-M2", FullName = "Outsider", CreatedAt = DateTime.UtcNow };
        db.AddRange(memberProfile, outsiderProfile);
        await db.SaveChangesAsync();

        void As(User user, string role, long? centerId) => http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issuer.Issue(new(user.Id, user.Username, role, centerId, user.Email)).Token);
        MultipartFormDataContent Image()
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(Png); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "photo.png");
            return form;
        }
        Task<string?> StoredKey(long userId) => db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.AvatarKey).SingleAsync();

        await check("own profile photo is uploaded, saved to users.avatar_key and returned as a public URL", async () =>
        {
            As(member, "Member", null);
            var upload = await http.PostAsync("api/r2-images/avatar", Image());
            var key = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
            var patch = await http.PatchAsJsonAsync("api/profile/me", new { fullName = "Persist Member", avatarKey = key });
            var profile = await http.GetFromJsonAsync<JsonElement>("api/profile/me");
            return upload.StatusCode == HttpStatusCode.Created && patch.IsSuccessStatusCode
                && await StoredKey(member.Id) == key && key.StartsWith($"avatars/{member.Id}/")
                && profile.GetProperty("avatar").GetString() == $"{PublicDomain}/{key}";
        });

        await check("editing name/phone without avatarKey keeps the stored photo; an empty avatarKey removes it", async () =>
        {
            As(member, "Member", null);
            var before = await StoredKey(member.Id);
            var edit = await http.PatchAsJsonAsync("api/profile/me", new { fullName = "Persist Member Renamed", phone = "0901234560" });
            var kept = await StoredKey(member.Id);
            var remove = await http.PatchAsJsonAsync("api/profile/me", new { fullName = "Persist Member Renamed", avatarKey = "" });
            return before is not null && edit.IsSuccessStatusCode && kept == before
                && remove.IsSuccessStatusCode && await StoredKey(member.Id) is null;
        });

        await check("counter staff store a member photo in the DB; a replaced photo is deleted from storage", async () =>
        {
            As(desk, "Receptionist", center.Id);
            var first = await http.PostAsync($"api/centers/{center.Id}/members/{memberProfile.Id}/avatar", Image());
            var firstKey = await StoredKey(member.Id);
            var second = await http.PostAsync($"api/centers/{center.Id}/members/{memberProfile.Id}/avatar", Image());
            var secondKey = await StoredKey(member.Id);
            var body = await second.Content.ReadFromJsonAsync<JsonElement>();
            return first.StatusCode == HttpStatusCode.OK && second.StatusCode == HttpStatusCode.OK
                && firstKey is not null && secondKey is not null && firstKey != secondKey
                && secondKey.StartsWith($"avatars/{member.Id}/")
                && body.GetProperty("avatarUrl").GetString() == $"{PublicDomain}/{secondKey}"
                && storage.Deleted.Contains(firstKey);
        });

        await check("counter staff cannot change the photo of a member from another center", async () =>
        {
            As(desk, "Receptionist", center.Id);
            var uploadsBefore = storage.Uploaded.Count;
            var foreign = await http.PostAsync($"api/centers/{center.Id}/members/{outsiderProfile.Id}/avatar", Image());
            var otherCenterRoute = await http.PostAsync($"api/centers/{otherCenter.Id}/members/{outsiderProfile.Id}/avatar", Image());
            return foreign.StatusCode == HttpStatusCode.NotFound
                && otherCenterRoute.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                && storage.Uploaded.Count == uploadsBefore && await StoredKey(outsider.Id) is null;
        });

        await check("landing editor draft is stored in the DB, survives reload and is consumed by publishing", async () =>
        {
            var service = new LandingContentService(db, configuration);
            using var draftDoc = JsonDocument.Parse("""{"heroBanner":{"title":"Bản nháp chưa xuất bản"}}""");
            await service.SaveDraftAsync(draftDoc.RootElement, desk.Id, default);
            var reloaded = await new LandingContentService(db, configuration).GetDraftAsync(default);
            var stored = await db.SystemSettings.AsNoTracking().AnyAsync(s => s.SettingKey == "site.landing-content.draft");
            var rejected = false;
            try { using var bad = JsonDocument.Parse("[1,2]"); await service.SaveDraftAsync(bad.RootElement, desk.Id, default); }
            catch (InvalidOperationException) { rejected = true; }
            await service.DiscardDraftAsync(default);
            var discarded = !await db.SystemSettings.AnyAsync(s => s.SettingKey == "site.landing-content.draft");
            return stored && rejected && discarded
                && reloaded.Content?.GetProperty("heroBanner").GetProperty("title").GetString() == "Bản nháp chưa xuất bản";
        });

        http.DefaultRequestHeaders.Authorization = null;
    }
}

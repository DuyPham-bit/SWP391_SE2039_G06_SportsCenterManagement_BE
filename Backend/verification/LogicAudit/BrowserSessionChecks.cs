using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

static class BrowserSessionChecks
{
    public static async Task Run(SportsCenterDbContext db, Uri address, FakeMailGateway mail, long centerId,
        Func<string, Func<Task<bool>>, Task> check)
    {
        const string first = "11111111111111111111111111111111", second = "22222222222222222222222222222222";
        const string password = "Browser_Audit42!", newPassword = "Browser_Changed43!";
        var roleId = await db.Roles.Where(r => r.Name == "Member").Select(r => r.Id).SingleAsync();
        User Account(string name) => new() {
            Username = name, Email = name + "@example.test", RoleId = roleId,
            PasswordHash = PasswordHashing.Hash(password), Status = "Active", CreatedAt = DateTime.UtcNow
        };
        var alice = Account("browser-alice"); var bob = Account("browser-bob");
        db.AddRange(alice, bob); await db.SaveChangesAsync();
        db.MemberProfiles.AddRange(new[] { alice, bob }.Select(u => new MemberProfile {
            UserId = u.Id, CenterId = centerId, MemberCode = "BROWSER-" + u.Id, FullName = u.Username, CreatedAt = DateTime.UtcNow
        }));
        await db.SaveChangesAsync();
        var cookies = new CookieContainer();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = address };
        using var raw = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = address };
        var apiAddress = new Uri(address, "/api/");
        Cookie? CookieFor(string id) => cookies.GetCookies(apiAddress).Cast<Cookie>().SingleOrDefault(c => c.Name == "scms.session." + id);
        async Task<HttpResponseMessage> Send(HttpClient target, string path, string? id, object? body = null, string? cookie = null)
        {
            using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, path);
            if (id is not null) request.Headers.Add(BrowserSessionCookie.HeaderName, id);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (cookie is not null) request.Headers.Add("Cookie", cookie);
            return await target.SendAsync(request);
        }
        Task<HttpResponseMessage> Login(User user, string id) => Send(client, "api/auth/login", id, new { email = user.Email, password });
        async Task<bool> Profile(string id, long expectedId) {
            using var response = await Send(client, "api/profile/me", id);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Profile returned HTTP {(int)response.StatusCode}.");
            return response.Headers.CacheControl?.NoStore == true
                && (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetInt64() == expectedId;
        }

        await check("browser login puts JWTs only in separate HttpOnly cookies with no token in JSON", async () => {
            using var a = await Login(alice, first); using var b = await Login(bob, second);
            var aBody = await a.Content.ReadFromJsonAsync<JsonElement>();
            var bBody = await b.Content.ReadFromJsonAsync<JsonElement>();
            return a.IsSuccessStatusCode && b.IsSuccessStatusCode
                && aBody.GetProperty("accessToken").ValueKind == JsonValueKind.Null && bBody.GetProperty("accessToken").ValueKind == JsonValueKind.Null
                && CookieFor(first) is { HttpOnly: true, Path: "/api" } && CookieFor(second) is { HttpOnly: true, Path: "/api" }
                && a.Headers.CacheControl?.NoStore == true
                && a.Headers.GetValues("Set-Cookie").Single().Contains("samesite=strict", StringComparison.OrdinalIgnoreCase);
        });
        await check("two tabs sharing the entire cookie jar retain different accounts after reload", async () =>
            await Profile(first, alice.Id) && await Profile(second, bob.Id) && await Profile(first, alice.Id));
        await check("a cookie alone, a forged selector or an invalid JWT cannot authenticate", async () => {
            using var noHeader = await Send(client, "api/profile/me", null);
            using var forged = await Send(client, "api/profile/me", new string('3', 32));
            using var invalidId = await Send(client, "api/profile/me", "invalid-id");
            using var tampered = await Send(raw, "api/profile/me", first, cookie: "scms.session." + first + "=invalid.jwt.value");
            return new[] { noHeader, forged, invalidId, tampered }.All(r => r.StatusCode == HttpStatusCode.Unauthorized);
        });
        await check("HTTPS browser cookies have Secure, HttpOnly, SameSite and no Domain attribute", () => {
            var context = new DefaultHttpContext(); context.Request.Scheme = "https";
            context.Request.Headers[BrowserSessionCookie.HeaderName] = first;
            BrowserSessionCookie.Issue(context, "audit-only", alice.Id, "MEMBER", DateTimeOffset.UtcNow.AddMinutes(5));
            var header = context.Response.Headers.SetCookie.ToString();
            return Task.FromResult(header.Contains("secure", StringComparison.OrdinalIgnoreCase)
                && header.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && header.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)
                && !header.Contains("domain=", StringComparison.OrdinalIgnoreCase));
        });
        await check("changing a password rotates its HttpOnly JWT and keeps the other account's tab usable", async () => {
            var oldCookie = "scms.session." + first + "=" + CookieFor(first)!.Value;
            using var otp = await Send(client, "api/auth/request-change-password-otp", first, new { currentPassword = password, newPassword });
            if (!otp.IsSuccessStatusCode) return false;
            using var changed = await Send(client, "api/auth/change-password", first, new { currentPassword = password, newPassword, emailOtp = mail.Otp });
            if (!changed.IsSuccessStatusCode) return false;
            var body = await changed.Content.ReadFromJsonAsync<JsonElement>();
            using var old = await Send(raw, "api/profile/me", first, cookie: oldCookie);
            return body.GetProperty("accessToken").ValueKind == JsonValueKind.Null
                && old.StatusCode == HttpStatusCode.Unauthorized && await Profile(first, alice.Id) && await Profile(second, bob.Id);
        });
        await check("disabled accounts are rejected even when the browser retains its cookie", async () => {
            await db.Users.Where(u => u.Id == alice.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Disabled"));
            try { return (await Send(client, "api/profile/me", first)).StatusCode == HttpStatusCode.Unauthorized && await Profile(second, bob.Id); }
            finally { await db.Users.Where(u => u.Id == alice.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Active")); }
        });
        await check("logout deletes only the selected tab's cookie", async () => {
            using var logout = await Send(client, "api/auth/logout", first, new { });
            return logout.StatusCode == HttpStatusCode.NoContent && CookieFor(first) is null && CookieFor(second) is not null
                && (await Send(client, "api/profile/me", first)).StatusCode == HttpStatusCode.Unauthorized && await Profile(second, bob.Id);
        });
    }
}

// Profile reads do not use storage; fail explicitly if a verification accidentally attempts an external write.
sealed class UnusedAuditStorage : SportsCenterManagement.API.Services.IR2StorageService
{
    public Task<string> UploadImageAsync(IFormFile file, string folder, long maxFileSize, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Media writes are outside this isolated audit.");
    public Task<string> UploadRefundBillAsync(IFormFile file, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Media writes are outside this isolated audit.");
    public Task<string> UploadMediaAsync(IFormFile file, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Media writes are outside this isolated audit.");
    public Task<SportsCenterManagement.API.Services.LegacyMediaMigrationResult> MigrateLegacyMediaAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Media writes are outside this isolated audit.");
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Media writes are outside this isolated audit.");
    public string GetPresignedUrl(string key, TimeSpan expiresIn)
        => throw new InvalidOperationException("Media reads are outside this isolated audit.");
}

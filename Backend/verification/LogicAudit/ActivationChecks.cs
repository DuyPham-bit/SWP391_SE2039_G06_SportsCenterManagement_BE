using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

/// <summary>Counter quick-create → phone default password → email link verify + change password.</summary>
static class ActivationChecks
{
    public static async Task Run(SportsCenterDbContext db, HttpClient http, JwtTokenIssuer issuer,
        FakeMailGateway mail, IConfiguration configuration, Func<string, Func<Task<bool>>, Task> check)
    {
        configuration["Frontend:BaseUrl"] = "https://app.example.test/";
        const string newPassword = "Activate_Audit42!";

        // Fixture: a receptionist assigned to an active center, with the counter-create permission.
        var center = new Center { Name = "Activation center", Address = "Audit", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Centers.Add(center);
        var receptionistRole = await db.Roles.SingleAsync(r => r.Name == "Receptionist");
        var memberRole = await db.Roles.SingleAsync(r => r.Name == "Member");
        var receptionist = new User { Username = "activation-desk", Email = "activation-desk@example.test", PasswordHash = "test-only",
            RoleId = receptionistRole.Id, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Users.Add(receptionist);
        await db.SaveChangesAsync();
        db.StaffProfiles.Add(new StaffProfile { UserId = receptionist.Id, CenterId = center.Id, StaffCode = "AUDIT-ACTIVATION",
            FullName = "Activation desk", Position = "Receptionist", Status = "Active", CreatedAt = DateTime.UtcNow });
        foreach (var code in new[] { PermissionCodes.MemberCenterCreate, PermissionCodes.MemberCenterRead })
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Code == code);
            if (permission is null)
            {
                permission = new Permission { Code = code, Name = code };
                db.Permissions.Add(permission);
                await db.SaveChangesAsync();
            }
            if (!await db.RolePermissions.AnyAsync(rp => rp.RoleId == receptionistRole.Id && rp.PermissionId == permission.Id))
                db.RolePermissions.Add(new RolePermission { RoleId = receptionistRole.Id, PermissionId = permission.Id });
            await db.SaveChangesAsync();
        }

        void AsReceptionist() => http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            issuer.Issue(new(receptionist.Id, receptionist.Username, "Receptionist", center.Id, receptionist.Email)).Token);
        void Bearer(string? token) => http.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        Task<HttpResponseMessage> QuickCreate(string fullName, string phone, string email) =>
            http.PostAsJsonAsync("api/receptionist/members/quick-create", new { fullName, phoneNumber = phone, email, gender = "Female" });
        async Task<string?> LoginToken(string login, string password)
        {
            Bearer(null);
            var response = await http.PostAsJsonAsync("api/auth/login", new { email = login, password });
            return response.IsSuccessStatusCode
                ? (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()
                : null;
        }
        string LinkToken() => Regex.Match(mail.Messages[^1].GetProperty("message").GetString()!, @"verify-email\?token=([A-Za-z0-9_-]+)").Groups[1].Value;
        Task<HttpResponseMessage> Preview(string token) { Bearer(null); return http.PostAsJsonAsync("api/auth/verify-email/preview", new { token }); }
        Task<HttpResponseMessage> Complete(string token, string password, string confirm)
        { Bearer(null); return http.PostAsJsonAsync("api/auth/verify-and-change-password", new { token, newPassword = password, confirmPassword = confirm }); }

        const string phone = "0912345678";
        const string email = "quick.member@example.test";

        await check("activation quick-create rejects a non-Vietnamese phone without creating an account", async () =>
        {
            AsReceptionist();
            var response = await QuickCreate("Invalid Phone", "12345", "invalid.phone@example.test");
            return response.StatusCode == HttpStatusCode.BadRequest && !await db.Users.AnyAsync(u => u.Email == "invalid.phone@example.test");
        });

        await check("activation quick-create stores the phone as a hashed must-change password and emails a hashed 24h link", async () =>
        {
            AsReceptionist();
            var before = mail.Messages.Count;
            var response = await QuickCreate("Trần Thị Hội Viên", "091 234 5678", "Quick.Member@Example.test");
            if (response.StatusCode != HttpStatusCode.Created || mail.Messages.Count != before + 1) return false;
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var raw = LinkToken();
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            var token = await db.AccountActivationTokens.AsNoTracking().SingleAsync(t => t.UserId == user.Id);
            var html = mail.Messages[^1].GetProperty("html").GetString()!;
            return body.GetProperty("emailSent").GetBoolean()
                && !body.GetProperty("member").GetProperty("isEmailVerified").GetBoolean()
                && !(await response.Content.ReadAsStringAsync()).Contains(raw)
                && user.Username == phone && user.Phone == phone && user.MustChangePassword && !user.IsEmailVerified
                && user.PasswordHash != phone && PasswordHashing.Verify(phone, user.PasswordHash)
                && raw.Length >= 40 && token.TokenHash == MemberActivationService.HashToken(raw) && token.TokenHash != raw
                && token.ExpiresAt > DateTime.UtcNow.AddHours(23.9) && token.ExpiresAt <= DateTime.UtcNow.AddHours(24)
                && mail.Messages[^1].GetProperty("message").GetString()!.Contains("https://app.example.test/#/verify-email?token=")
                && html.Contains("MEMBERSHIP PORTAL") && html.Contains("#B38A65") && html.Contains("#F7F5F0")
                && html.Contains("XÁC NHẬN &amp; ĐỔI MẬT KHẨU NGAY") && html.Contains(phone)
                && await db.MemberProfiles.AnyAsync(p => p.UserId == user.Id && p.CenterId == center.Id && p.Gender == "Female");
        });

        await check("activation quick-create reports duplicate phone and duplicate email separately", async () =>
        {
            AsReceptionist();
            var dupPhone = await QuickCreate("Dup", phone, "other.member@example.test");
            var dupEmail = await QuickCreate("Dup", "0987654321", email);
            return dupPhone.StatusCode == HttpStatusCode.Conflict && dupEmail.StatusCode == HttpStatusCode.Conflict
                && (await dupPhone.Content.ReadAsStringAsync()).Contains("Số điện thoại")
                && (await dupEmail.Content.ReadAsStringAsync()).Contains("Email");
        });

        await check("activation member role cannot quick-create members", async () =>
        {
            var member = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            Bearer(issuer.Issue(new(member.Id, member.Username, "Member", null, member.Email)).Token);
            return (await QuickCreate("Forged", "0977000111", "forged@example.test")).StatusCode == HttpStatusCode.Forbidden;
        });

        string? phoneSession = null;
        await check("activation default phone login works but permission-guarded APIs stay blocked until the password changes", async () =>
        {
            phoneSession = await LoginToken(phone, phone);
            if (phoneSession is null) return false;
            Bearer(phoneSession);
            var profile = await http.GetFromJsonAsync<JsonElement>("api/profile/me");
            var memberId = await MemberIdByEmail(db, email);
            var guarded = await http.GetAsync($"api/members/{memberId}/subscriptions");
            return profile.GetProperty("mustChangePassword").GetBoolean() && !profile.GetProperty("isEmailVerified").GetBoolean()
                && guarded.StatusCode == HttpStatusCode.Forbidden;
        });

        await check("activation resend enforces a 60 second cooldown and replaces the previous link", async () =>
        {
            var oldRaw = LinkToken();
            var memberId = await MemberIdByEmail(db, email);
            AsReceptionist();
            var early = await http.PostAsync($"api/receptionist/members/{memberId}/resend-verification", null);
            if (early.StatusCode != HttpStatusCode.TooManyRequests || early.Headers.RetryAfter is null) return false;
            await db.AccountActivationTokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, DateTime.UtcNow.AddMinutes(-2)));
            var resent = await http.PostAsync($"api/receptionist/members/{memberId}/resend-verification", null);
            var newRaw = LinkToken();
            return resent.StatusCode == HttpStatusCode.OK && newRaw != oldRaw
                && (await Preview(oldRaw)).StatusCode == HttpStatusCode.BadRequest
                && (await Preview(newRaw)).StatusCode == HttpStatusCode.OK;
        });

        await check("activation preview greets the member and rejects unknown or expired links", async () =>
        {
            var preview = await (await Preview(LinkToken())).Content.ReadFromJsonAsync<JsonElement>();
            var unknown = await Preview("not-a-real-token");
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            var expired = new AccountActivationToken { UserId = user.Id, TokenHash = MemberActivationService.HashToken("expired-audit-token"),
                CreatedAt = DateTime.UtcNow.AddDays(-2), ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
            db.AccountActivationTokens.Add(expired);
            await db.SaveChangesAsync();
            return preview.GetProperty("fullName").GetString() == "Trần Thị Hội Viên" && preview.GetProperty("email").GetString() == email
                && preview.GetProperty("phone").GetString() == phone
                && unknown.StatusCode == HttpStatusCode.BadRequest
                && (await Preview("expired-audit-token")).StatusCode == HttpStatusCode.BadRequest;
        });

        await check("activation complete rejects mismatched confirmation, weak passwords and passwords containing the phone", async () =>
        {
            var raw = LinkToken();
            var mismatch = await Complete(raw, newPassword, newPassword + "x");
            var weak = await Complete(raw, "abc123", "abc123");
            var containsPhone = await Complete(raw, $"Aa!{phone}", $"Aa!{phone}");
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            return mismatch.StatusCode == HttpStatusCode.BadRequest && weak.StatusCode == HttpStatusCode.BadRequest
                && containsPhone.StatusCode == HttpStatusCode.BadRequest && user.MustChangePassword && !user.IsEmailVerified;
        });

        await check("activation complete verifies email, replaces the phone password, revokes old sessions and is single-use", async () =>
        {
            var raw = LinkToken();
            var before = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            var done = await Complete(raw, newPassword, newPassword);
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            Bearer(phoneSession);
            var oldSession = await http.GetAsync("api/profile/me");
            var replay = await Complete(raw, "Another_Audit43!", "Another_Audit43!");
            return done.StatusCode == HttpStatusCode.NoContent && user.IsEmailVerified && !user.MustChangePassword
                && user.Status == "Active" && user.AuthVersion == before.AuthVersion + 1
                && PasswordHashing.Verify(newPassword, user.PasswordHash)
                && oldSession.StatusCode == HttpStatusCode.Unauthorized
                && replay.StatusCode == HttpStatusCode.BadRequest
                && await LoginToken(phone, phone) is null
                && await LoginToken(email, newPassword) is not null
                && !await db.AccountActivationTokens.AnyAsync(t => t.UserId == user.Id && t.UsedAt == null);
        });

        await check("activation completion notifies the creating receptionist and flips the live status", async () =>
        {
            var memberId = await MemberIdByEmail(db, email);
            var notified = await (from link in db.UserNotifications
                                  join note in db.Notifications on link.NotificationId equals note.Id
                                  where link.UserId == receptionist.Id && note.NotificationType == "MemberActivated"
                                  select note.Message).ToListAsync();
            AsReceptionist();
            var status = await http.GetFromJsonAsync<JsonElement>($"api/receptionist/members/verification-status?ids={memberId}&ids=999999");
            return notified.Count == 1 && notified[0].Contains("Trần Thị Hội Viên")
                && status.GetArrayLength() == 1
                && status[0].GetProperty("memberId").GetInt64() == memberId
                && status[0].GetProperty("isEmailVerified").GetBoolean();
        });

        await check("activation resend is refused once the member has verified", async () =>
        {
            var memberId = await MemberIdByEmail(db, email);
            AsReceptionist();
            return (await http.PostAsync($"api/receptionist/members/{memberId}/resend-verification", null)).StatusCode == HttpStatusCode.Conflict;
        });

        await check("activation failed email delivery revokes the link and allows an immediate resend", async () =>
        {
            AsReceptionist();
            mail.Reject = true;
            HttpResponseMessage created;
            try { created = await QuickCreate("Mail Down", "0388000111", "mail.down@example.test"); }
            finally { mail.Reject = false; }
            var body = await created.Content.ReadFromJsonAsync<JsonElement>();
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "mail.down@example.test");
            var revoked = !await db.AccountActivationTokens.AnyAsync(t => t.UserId == user.Id && t.UsedAt == null);
            var memberId = body.GetProperty("member").GetProperty("memberId").GetInt64();
            var resent = await http.PostAsync($"api/receptionist/members/{memberId}/resend-verification", null);
            return created.StatusCode == HttpStatusCode.Created && !body.GetProperty("emailSent").GetBoolean()
                && revoked && resent.StatusCode == HttpStatusCode.OK;
        });

        await check("activation first-login fallback requires the current phone password and issues a fresh session", async () =>
        {
            const string fallbackPhone = "0388000111";
            var session = await LoginToken(fallbackPhone, fallbackPhone);
            if (session is null) return false;
            Bearer(session);
            var wrong = await http.PostAsJsonAsync("api/auth/initial-password",
                new { currentPassword = "0000000000", newPassword, confirmPassword = newPassword });
            var ok = await http.PostAsJsonAsync("api/auth/initial-password",
                new { currentPassword = fallbackPhone, newPassword, confirmPassword = newPassword });
            var fresh = ok.IsSuccessStatusCode
                ? (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString() : null;
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "mail.down@example.test");
            Bearer(fresh);
            var again = await http.PostAsJsonAsync("api/auth/initial-password",
                new { currentPassword = newPassword, newPassword = "Another_Audit43!", confirmPassword = "Another_Audit43!" });
            return wrong.StatusCode == HttpStatusCode.BadRequest && ok.StatusCode == HttpStatusCode.OK && fresh is not null
                && !user.MustChangePassword && !user.IsEmailVerified
                && PasswordHashing.Verify(newPassword, user.PasswordHash)
                && again.StatusCode == HttpStatusCode.Conflict;
        });

        Bearer(null);
    }

    private static Task<long> MemberIdByEmail(SportsCenterDbContext db, string email) =>
        (from profile in db.MemberProfiles join user in db.Users on profile.UserId equals user.Id
         where user.Email == email select profile.Id).SingleAsync();
}

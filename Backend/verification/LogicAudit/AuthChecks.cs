using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

sealed class FakeMailGateway
{
    public List<JsonElement> Messages { get; } = [];
    public bool Reject { get; set; }
    public string? IdempotencyKey { get; private set; }
    public string? Authorization { get; private set; }
    public TaskCompletionSource<bool>? Arrived { get; set; }
    public TaskCompletionSource<bool>? HoldResponse { get; set; }
    public async Task Receive(HttpContext context)
    {
        using var document = await JsonDocument.ParseAsync(context.Request.Body);
        Messages.Add(document.RootElement.Clone());
        IdempotencyKey = context.Request.Headers["Idempotency-Key"];
        Authorization = context.Request.Headers.Authorization;
        Arrived?.TrySetResult(true);
        if (HoldResponse is { } pending) await pending.Task.WaitAsync(context.RequestAborted);
        context.Response.StatusCode = Reject ? 503 : 200;
        // A provider may echo sensitive payloads; production code must never persist this body.
        await context.Response.WriteAsync(Reject ? "sensitive-provider-response" : "accepted");
    }
    public string Otp => Regex.Match(Messages[^1].GetProperty("message").GetString()!, @"\b[0-9]{6}\b").Value;
}

static class AuthChecks
{
    public static async Task Run(SportsCenterDbContext db, HttpClient http, JwtTokenIssuer issuer,
        User owner, FakeMailGateway mail, Func<string, Func<Task<bool>>, Task> check)
    {
        const string password = "Reset_Audit42!";
        const string laterPassword = "Later_Audit43!";
        var oldToken = issuer.Issue(new(owner.Id, owner.Username, "Member", null, owner.Email)).Token;
        http.DefaultRequestHeaders.Authorization = null;
        string otp = "";
        Task<HttpResponseMessage> Issue(string? email = null) => http.PostAsJsonAsync("api/auth/request-password-reset", new { email = email ?? owner.Email });
        Task<HttpResponseMessage> Reset(string code, string secret = password) => http.PostAsJsonAsync("api/auth/reset-password", new { email = owner.Email, otp = code, newPassword = secret });
        async Task<PasswordResetToken> Latest() => await db.PasswordResetTokens.AsNoTracking()
            .Where(t => t.UserId == owner.Id).OrderByDescending(t => t.Id).FirstAsync();
        async Task<HttpResponseMessage> Profile(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/members/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await http.SendAsync(request);
        }

        await check("HTTP reset email is delivered without exposing OTP in the production response or inbox", async () => {
            var response = await Issue();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            otp = mail.Otp;
            var token = await Latest();
            var delivery = await db.CommunicationDeliveries.AsNoTracking().OrderByDescending(d => d.Id).FirstAsync();
            return response.StatusCode == HttpStatusCode.OK && body.GetProperty("developmentOtp").ValueKind == JsonValueKind.Null
                && otp.Length == 6 && token.Token != otp && BCrypt.Net.BCrypt.Verify(otp, token.Token)
                && mail.Messages[^1].GetProperty("to").GetString() == owner.Email
                && mail.IdempotencyKey == $"password-reset-{token.Id}" && mail.Authorization == "Bearer audit-only-token"
                && delivery.Status == "Sent" && delivery.SentAt != null && delivery.NotificationId == null
                && !await db.Notifications.AnyAsync(n => n.NotificationType == "PasswordReset");
        });
        await check("HTTP malformed reset email and OTP fail validation", async () =>
            (await Issue("not-an-email")).StatusCode == HttpStatusCode.BadRequest
            && (await Reset("abc")).StatusCode == HttpStatusCode.BadRequest);
        await check("HTTP wrong OTP increments its persistent attempt count", async () =>
            (await Reset("000000")).StatusCode == HttpStatusCode.BadRequest && (await Latest()).FailedAttempts == 1);
        await check("HTTP weak and non-ASCII passwords are rejected without consuming a valid OTP", async () =>
            (await Reset(otp, "weakpass")).StatusCode == HttpStatusCode.BadRequest
            && (await Reset(otp, "MậtKhẩu_42!")).StatusCode == HttpStatusCode.BadRequest
            && (await Latest()).UsedAt == null);
        await check("HTTP valid reset changes the hash and revokes all previously issued JWTs", async () => {
            var result = await Reset(otp);
            var account = await db.Users.AsNoTracking().SingleAsync(u => u.Id == owner.Id);
            return result.StatusCode == HttpStatusCode.NoContent && account.AuthVersion == 1
                && PasswordHashing.Verify(password, account.PasswordHash)
                && (await Latest()).UsedAt != null && (await Profile(oldToken)).StatusCode == HttpStatusCode.Unauthorized;
        });
        await check("HTTP a consumed OTP cannot be replayed", async () =>
            (await Reset(otp, laterPassword)).StatusCode == HttpStatusCode.BadRequest);
        string freshToken = "";
        await check("HTTP login after reset issues a usable JWT with the new account version", async () => {
            var response = await http.PostAsJsonAsync("api/auth/login", new { email = owner.Email, password });
            if (!response.IsSuccessStatusCode) return false;
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            freshToken = body.GetProperty("accessToken").GetString()!;
            return (await Profile(freshToken)).IsSuccessStatusCode;
        });
        await check("HTTP disabled account reports that it is locked after valid password verification", async () => {
            await db.Users.Where(u => u.Id == owner.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Disabled"));
            try {
                var response = await http.PostAsJsonAsync("api/auth/login", new { email = owner.Email, password });
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                return (int)response.StatusCode == 423
                    && body.GetProperty("code").GetString() == "ACCOUNT_DISABLED"
                    && body.GetProperty("message").GetString() == "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";
            } finally {
                await db.Users.Where(u => u.Id == owner.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Active"));
            }
        });
        await check("HTTP disabled account with a wrong password still reports invalid credentials", async () => {
            await db.Users.Where(u => u.Id == owner.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Disabled"));
            try {
                var response = await http.PostAsJsonAsync("api/auth/login", new { email = owner.Email, password = "Wrong_Password42!" });
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                return response.StatusCode == HttpStatusCode.Unauthorized
                    && body.GetProperty("message").GetString() == "Email/số điện thoại hoặc mật khẩu không chính xác.";
            } finally {
                await db.Users.Where(u => u.Id == owner.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Active"));
            }
        });
        await check("HTTP five wrong OTP attempts consume the token and reject the correct code", async () => {
            if (!(await Issue()).IsSuccessStatusCode) return false;
            var code = mail.Otp;
            for (var attempt = 0; attempt < 5; attempt++)
                if ((await Reset("000000")).StatusCode != HttpStatusCode.BadRequest) return false;
            var token = await Latest();
            return token.FailedAttempts == 5 && token.UsedAt != null
                && (await Reset(code, laterPassword)).StatusCode == HttpStatusCode.BadRequest;
        });
        await check("HTTP an expired OTP is rejected", async () => {
            await Issue(); var code = mail.Otp; var token = await Latest();
            await db.PasswordResetTokens.Where(t => t.Id == token.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            return (await Reset(code, laterPassword)).StatusCode == HttpStatusCode.BadRequest;
        });
        await check("HTTP reissuing an OTP invalidates every prior outstanding token", async () => {
            await Issue(); var previous = await Latest(); await Issue();
            var current = await Latest();
            return previous.Id != current.Id && current.UsedAt == null
                && !await db.PasswordResetTokens.AnyAsync(t => t.UserId == owner.Id && t.Id != current.Id && t.UsedAt == null);
        });
        await check("HTTP email gateway failure returns 503, consumes the OTP and stores no provider body", async () => {
            mail.Reject = true;
            try {
                var response = await Issue();
                var delivery = await db.CommunicationDeliveries.AsNoTracking().OrderByDescending(d => d.Id).FirstAsync();
                return response.StatusCode == HttpStatusCode.ServiceUnavailable && (await Latest()).UsedAt != null
                    && delivery.Status == "Failed" && delivery.ProviderMessage == "HTTP 503";
            } finally { mail.Reject = false; }
        });
        await check("HTTP concurrent resets with one OTP commit exactly one password change", async () => {
            await Issue(); var code = mail.Otp;
            var before = await db.Users.AsNoTracking().Where(u => u.Id == owner.Id).Select(u => u.AuthVersion).SingleAsync();
            var results = await Task.WhenAll(Reset(code, laterPassword), Reset(code, laterPassword));
            var after = await db.Users.AsNoTracking().Where(u => u.Id == owner.Id).Select(u => u.AuthVersion).SingleAsync();
            return results.Count(r => r.StatusCode == HttpStatusCode.NoContent) == 1 && after == before + 1
                && results.All(r => r.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest or HttpStatusCode.ServiceUnavailable)
                && (await Profile(freshToken)).StatusCode == HttpStatusCode.Unauthorized;
        });
        var longPassword = new string('A', 120) + "a42!tail";
        string changeToken = "";
        string beforeChangeOtp = "";
        await check("HTTP change-password supports passwords longer than 72 bytes and rotates JWTs", async () => {
            await Issue(); beforeChangeOtp = mail.Otp;
            var login = await http.PostAsJsonAsync("api/auth/login", new { email = owner.Email, password = laterPassword });
            var body = await login.Content.ReadFromJsonAsync<JsonElement>();
            var token = body.GetProperty("accessToken").GetString()!;
            using var otpRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/request-change-password-otp") {
                Content = JsonContent.Create(new { currentPassword = laterPassword, newPassword = longPassword })
            };
            otpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!(await http.SendAsync(otpRequest)).IsSuccessStatusCode) return false;
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/change-password") {
                Content = JsonContent.Create(new { currentPassword = laterPassword, newPassword = longPassword, emailOtp = mail.Otp })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            changeToken = result.GetProperty("accessToken").GetString()!;
            var account = await db.Users.AsNoTracking().SingleAsync(u => u.Id == owner.Id);
            return PasswordHashing.Verify(longPassword, account.PasswordHash)
                && !PasswordHashing.Verify(new string('A', 72) + "different1!", account.PasswordHash)
                && (await Profile(token)).StatusCode == HttpStatusCode.Unauthorized
                && (await Profile(changeToken)).IsSuccessStatusCode;
        });
        await check("HTTP changing a password also invalidates every earlier reset OTP", async () =>
            !await db.PasswordResetTokens.AnyAsync(t => t.UserId == owner.Id && t.UsedAt == null)
            && (await Reset(beforeChangeOtp, password)).StatusCode == HttpStatusCode.BadRequest);
        await check("HTTP wrong current password cannot change credentials or revoke the current session", async () => {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/change-password") {
                Content = JsonContent.Create(new { currentPassword = "Wrong_Current42!", newPassword = password })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", changeToken);
            return (await http.SendAsync(request)).StatusCode == HttpStatusCode.BadRequest
                && (await Profile(changeToken)).IsSuccessStatusCode;
        });
        await check("HTTP cancelling an in-flight reset email consumes its OTP and finalizes delivery state", async () => {
            mail.Arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            mail.HoldResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource();
            try {
                var pending = http.PostAsJsonAsync("api/auth/request-password-reset", new { email = owner.Email }, cancellation.Token);
                await mail.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var token = await Latest();
                cancellation.Cancel();
                try { await pending; return false; } catch (OperationCanceledException) { }
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline) {
                    var consumed = await db.PasswordResetTokens.AsNoTracking().AnyAsync(t => t.Id == token.Id && t.UsedAt != null);
                    var delivery = await db.CommunicationDeliveries.AsNoTracking().OrderByDescending(d => d.Id).FirstAsync();
                    if (consumed && delivery.Status == "Failed") return true;
                    await Task.Delay(50);
                }
                return false;
            } finally {
                cancellation.Cancel();
                mail.HoldResponse.TrySetResult(true);
                mail.HoldResponse = null;
                mail.Arrived = null;
            }
        });
        await check("HTTP unknown and disabled accounts do not create OTPs or disclose their state", async () => {
            var total = await db.PasswordResetTokens.CountAsync(); var delivered = mail.Messages.Count;
            var unknown = await Issue("unknown@example.test");
            await db.Users.Where(u => u.Id == owner.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Inactive"));
            try {
                var inactive = await Issue();
                return unknown.StatusCode == HttpStatusCode.OK && inactive.StatusCode == HttpStatusCode.OK
                    && await unknown.Content.ReadAsStringAsync() == await inactive.Content.ReadAsStringAsync()
                    && total == await db.PasswordResetTokens.CountAsync() && delivered == mail.Messages.Count
                    && (await Profile(changeToken)).StatusCode == HttpStatusCode.Unauthorized;
            } finally {
                await db.Users.Where(u => u.Id == owner.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Active"));
            }
        });
        await check("HTTP reset endpoints share a per-IP limit of ten requests per minute", async () => {
            for (var attempt = 0; attempt < 10; attempt++)
                if ((await Issue("unknown@example.test")).StatusCode != HttpStatusCode.OK) return false;
            return (await Reset("000000")).StatusCode == HttpStatusCode.TooManyRequests;
        });
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

static class RegistrationChecks
{
    public static async Task Run(SportsCenterDbContext db, HttpClient http, FakeMailGateway mail,
        IConfiguration configuration, long centerId, Func<string, Func<Task<bool>>, Task> check)
    {
        const string email = "registration@example.test";
        const string password = "Register_Audit42!";
        http.DefaultRequestHeaders.Authorization = null;
        Task<HttpResponseMessage> Issue(string address = email) => http.PostAsJsonAsync("api/auth/request-email-verification", new { email = address });
        Task<HttpResponseMessage> RequestReset() => http.PostAsJsonAsync("api/auth/request-password-reset", new { email = "owner@example.test" });
        Task<HttpResponseMessage> Register(string? otp, string address = email, string username = "registration") =>
            http.PostAsJsonAsync("api/auth/register", new { username, email = address, password, fullName = "Email audit member", centerId, emailOtp = otp });
        Task<EmailVerificationToken> Latest() => db.EmailVerificationTokens.AsNoTracking().Where(t => t.Email == email).OrderByDescending(t => t.Id).FirstAsync();
        Task AllowResend() => db.EmailVerificationTokens.Where(t => t.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, DateTime.UtcNow.AddMinutes(-2)));

        await check("anonymous registration cannot disclose an existing email or username before OTP verification", async () => {
            var unknown = await Register(null);
            var knownEmail = await Register(null, "owner@example.test", "privacy-probe");
            var knownUsername = await Register(null, "privacy-probe@example.test", "owner");
            var body = await unknown.Content.ReadAsStringAsync();
            return unknown.StatusCode == HttpStatusCode.BadRequest
                && knownEmail.StatusCode == unknown.StatusCode && knownUsername.StatusCode == unknown.StatusCode
                && await knownEmail.Content.ReadAsStringAsync() == body
                && await knownUsername.Content.ReadAsStringAsync() == body;
        });
        await check("registration rejects a missing OTP without creating a user or profile", async () => {
            var profiles = await db.MemberProfiles.CountAsync();
            return (await Register(null)).StatusCode == HttpStatusCode.BadRequest
                && !await db.Users.AnyAsync(u => u.Email == email) && profiles == await db.MemberProfiles.CountAsync();
        });
        await check("registration fails closed when email delivery is not configured", async () => {
            var endpoint = configuration["ExternalCommunications:Email:WebhookUrl"];
            configuration["ExternalCommunications:Email:WebhookUrl"] = "";
            try { return (await Issue()).StatusCode == HttpStatusCode.ServiceUnavailable && !await db.EmailVerificationTokens.AnyAsync(); }
            finally { configuration["ExternalCommunications:Email:WebhookUrl"] = endpoint; }
        });
        string code = "";
        await check("registration emails a hashed single-purpose OTP and never returns it to the client", async () => {
            var response = await Issue("  REGISTRATION@example.test  ");
            if (response.StatusCode != HttpStatusCode.OK) return false;
            code = mail.Otp;
            var token = await Latest();
            var body = await response.Content.ReadAsStringAsync();
            return code.Length == 6 && token.Token != code && BCrypt.Net.BCrypt.Verify(code, token.Token)
                && token.ExpiresAt > DateTime.UtcNow.AddMinutes(9) && !body.Contains(code)
                && mail.Messages[^1].GetProperty("notificationType").GetString() == "EmailVerification"
                && mail.IdempotencyKey == $"email-verification-{token.Id}"
                && !await db.Users.AnyAsync(u => u.Email == email);
        });
        await check("registration throttles resends for the same email even from another caller IP", async () =>
            (await Issue()).StatusCode == HttpStatusCode.TooManyRequests && await db.EmailVerificationTokens.CountAsync() == 1);
        await check("registration OTP cannot reset a password or verify another email", async () =>
            (await http.PostAsJsonAsync("api/auth/reset-password", new { email, otp = code, newPassword = password })).StatusCode == HttpStatusCode.BadRequest
            && (await Register(code, "other-registration@example.test", "other-registration")).StatusCode == HttpStatusCode.BadRequest);
        await check("registration records wrong attempts and locks the OTP after five failures", async () => {
            for (var i = 0; i < 5; i++)
                if ((await Register("000000")).StatusCode != HttpStatusCode.BadRequest) return false;
            var token = await Latest();
            return token.FailedAttempts == 5 && token.UsedAt != null && (await Register(code)).StatusCode == HttpStatusCode.BadRequest;
        });
        await check("registration rejects an expired code", async () => {
            await AllowResend(); await Issue(); code = mail.Otp;
            await db.EmailVerificationTokens.Where(t => t.Email == email).ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            return (await Register(code)).StatusCode == HttpStatusCode.BadRequest && !await db.Users.AnyAsync(u => u.Email == email);
        });
        await check("registration resending invalidates old tokens", async () => {
            await AllowResend(); await Issue(); var old = await Latest();
            await AllowResend(); await Issue(); code = mail.Otp;
            var fresh = await Latest();
            return fresh.Id != old.Id && !await db.EmailVerificationTokens.AnyAsync(t => t.Email == email && t.Id != fresh.Id && t.UsedAt == null);
        });
        await check("registration email provider failure invalidates its code and does not throttle immediate retry", async () => {
            await AllowResend(); mail.Reject = true;
            try {
                var response = await Issue();
                if (response.StatusCode != HttpStatusCode.ServiceUnavailable || (await Latest()).UsedAt == null
                    || await db.Users.AnyAsync(u => u.Email == email)) return false;
            } finally { mail.Reject = false; }
            var retry = await Issue();
            return retry.StatusCode == HttpStatusCode.OK && (await Latest()).UsedAt == null;
        });
        await check("registration creates an active member only after valid email verification", async () => {
            await AllowResend(); await Issue(); code = mail.Otp;
            var response = await Register(code);
            var account = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email);
            return response.StatusCode == HttpStatusCode.Created && account is not null
                && account.Status == "Active" && PasswordHashing.Verify(password, account.PasswordHash)
                && (await Latest()).UsedAt != null && await db.MemberProfiles.AnyAsync(p => p.UserId == account.Id && p.CenterId == centerId);
        });
        await check("registration OTP cannot be replayed after removing the created fixture account", async () => {
            // Keep the consumed challenge while changing the email on this isolated account.
            await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, "registered-fixture@example.test").SetProperty(u => u.Username, "registered-fixture"));
            return (await Register(code)).StatusCode == HttpStatusCode.BadRequest && !await db.Users.AnyAsync(u => u.Email == email);
        });
        await check("two concurrent registrations consume one email challenge at most once", async () => {
            await AllowResend(); await Issue(); code = mail.Otp;
            var results = await Task.WhenAll(Register(code, username: "concurrent-a"), Register(code, username: "concurrent-b"));
            return results.Count(r => r.StatusCode == HttpStatusCode.Created) == 1
                && await db.Users.CountAsync(u => u.Email == email) == 1 && (await Latest()).UsedAt != null;
        });
        await check("verification requests and resend limits do not reveal whether an email is registered", async () => {
            const string known = "owner@example.test", unknown = "privacy-check@example.test";
            var existing = await Issue(known);
            var fresh = await Issue(unknown);
            if (existing.StatusCode != HttpStatusCode.OK || fresh.StatusCode != existing.StatusCode
                || await existing.Content.ReadAsStringAsync() != await fresh.Content.ReadAsStringAsync()) return false;
            if (!await db.EmailVerificationTokens.AnyAsync(t => t.Email == known && t.UsedAt == null)
                || !await db.EmailVerificationTokens.AnyAsync(t => t.Email == unknown && t.UsedAt == null)) return false;
            var existingRetry = await Issue(known);
            var freshRetry = await Issue(unknown);
            return existingRetry.StatusCode == HttpStatusCode.TooManyRequests && freshRetry.StatusCode == existingRetry.StatusCode
                && existingRetry.Headers.RetryAfter?.Delta == freshRetry.Headers.RetryAfter?.Delta
                && await existingRetry.Content.ReadAsStringAsync() == await freshRetry.Content.ReadAsStringAsync();
        });
        await using (var smtp = new FakeSmtpServer())
        {
            var endpoint = configuration["ExternalCommunications:Email:WebhookUrl"];
            configuration["ExternalCommunications:Email:WebhookUrl"] = "";
            configuration["ExternalCommunications:Email:Smtp:Host"] = "127.0.0.1";
            configuration["ExternalCommunications:Email:Smtp:Port"] = smtp.Port.ToString();
            configuration["ExternalCommunications:Email:Smtp:From"] = "audit-sender@example.test";
            configuration["ExternalCommunications:Email:Smtp:EnableSsl"] = "false";
            try
            {
                await check("SMTP delivers a registration OTP that can create the account", async () => {
                    var response = await Issue("smtp-registration@example.test");
                    var otp = smtp.Otp;
                    return response.StatusCode == HttpStatusCode.OK && otp.Length == 6
                        && smtp.Recipient!.Contains("smtp-registration@example.test")
                        && (await Register(otp, "smtp-registration@example.test", "smtp-registration")).StatusCode == HttpStatusCode.Created;
                });
                await check("SMTP sends password reset codes without returning them in the response", async () => {
                    var response = await http.PostAsJsonAsync("api/auth/request-password-reset", new { email = "owner@example.test" });
                    var otp = smtp.Otp;
                    var body = await response.Content.ReadAsStringAsync();
                    return response.StatusCode == HttpStatusCode.OK && otp.Length == 6 && !body.Contains(otp)
                        && smtp.Recipient!.Contains("owner@example.test");
                });
                await check("SMTP rejection consumes the pending registration OTP and returns 503", async () => {
                    smtp.Reject = true;
                    var response = await Issue("smtp-rejected@example.test");
                    return response.StatusCode == HttpStatusCode.ServiceUnavailable
                        && await db.EmailVerificationTokens.AnyAsync(t => t.Email == "smtp-rejected@example.test" && t.UsedAt != null);
                });
            }
            finally
            {
                configuration["ExternalCommunications:Email:WebhookUrl"] = endpoint;
                foreach (var key in new[] { "Host", "Port", "From", "EnableSsl" }) configuration[$"ExternalCommunications:Email:Smtp:{key}"] = null;
            }
        }
        await check("registration and reset share an IP limit without disclosing OTPs", async () => {
            for (var i = 0; i < 10; i++)
                if ((await RequestReset()).StatusCode != HttpStatusCode.OK) return false;
            return (await Register("000000")).StatusCode == HttpStatusCode.TooManyRequests;
        });
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.Models;
using SportsCenterManagement.API.Services;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

sealed class RecordingMailService : ISendMailService
{
    public List<MailContent> Messages { get; } = [];
    public Task<string> SendMailAsync(MailContent content)
    {
        Messages.Add(content);
        return Task.FromResult("Isolated mail fixture accepted.");
    }
}

static class MailSecurityChecks
{
    public static async Task Run(SportsCenterDbContext db, HttpClient http, JwtTokenIssuer issuer,
        RecordingMailService mail, Func<string, Func<Task<bool>>, Task> check)
    {
        async Task<User> Account(string name, string roleName) {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == roleName);
            if (role is null) { role = new Role { Name = roleName }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { Username = name, Email = name + "@example.test", RoleId = role.Id,
                Status = "Active", PasswordHash = "not-a-login-credential", CreatedAt = DateTime.UtcNow };
            db.Users.Add(user); await db.SaveChangesAsync(); return user;
        }
        var member = await Account("mail-member", RoleNames.Member);
        var manager = await Account("mail-manager", RoleNames.Manager);
        var admin = await Account("mail-admin", RoleNames.Admin);
        var rateAdmin = await Account("mail-rate-admin", RoleNames.Admin);
        void Login(User user, string role) => http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", issuer.Issue(new(user.Id, user.Username, role, null, user.Email, user.AuthVersion)).Token);
        var message = new { to = "mail-recipient@example.test", subject = "Security audit", body = "Local fixture only" };

        await check("mail send rejects anonymous callers without invoking the email service", async () => {
            http.DefaultRequestHeaders.Authorization = null;
            var before = mail.Messages.Count;
            return (await http.PostAsJsonAsync("api/mail/send", message)).StatusCode == HttpStatusCode.Unauthorized
                && mail.Messages.Count == before;
        });
        await check("mail send rejects Member and Manager accounts without invoking the email service", async () => {
            var before = mail.Messages.Count;
            Login(member, RoleNames.Member);
            var a = await http.PostAsJsonAsync("api/mail/send", message);
            Login(manager, RoleNames.Manager);
            var b = await http.PostAsJsonAsync("api/mail/send", message);
            return a.StatusCode == HttpStatusCode.Forbidden && b.StatusCode == HttpStatusCode.Forbidden && mail.Messages.Count == before;
        });
        await check("mail send validates Admin input and never substitutes a default recipient", async () => {
            Login(admin, RoleNames.Admin);
            var before = mail.Messages.Count;
            var malformed = await http.PostAsJsonAsync("api/mail/send", new { to = "not-an-email", subject = "Subject", body = "Body" });
            var empty = await http.PostAsJsonAsync("api/mail/send", new { });
            var oversized = await http.PostAsJsonAsync("api/mail/send", new { message.to, subject = new string('x', 201), message.body });
            return new[] { malformed, empty, oversized }.All(r => r.StatusCode == HttpStatusCode.BadRequest) && mail.Messages.Count == before;
        });
        await check("mail send preserves the authorized Admin request", async () => {
            Login(admin, RoleNames.Admin);
            var before = mail.Messages.Count;
            var response = await http.PostAsJsonAsync("api/mail/send", message);
            return response.StatusCode == HttpStatusCode.OK && mail.Messages.Count == before + 1
                && mail.Messages[^1].To == message.to && mail.Messages[^1].Subject == message.subject && mail.Messages[^1].Body == message.body;
        });
        await check("mail send limits an Admin to five requests per minute", async () => {
            Login(rateAdmin, RoleNames.Admin);
            var before = mail.Messages.Count;
            for (var i = 0; i < 5; i++)
                if ((await http.PostAsJsonAsync("api/mail/send", message)).StatusCode != HttpStatusCode.OK) return false;
            return (await http.PostAsJsonAsync("api/mail/send", message)).StatusCode == HttpStatusCode.TooManyRequests
                && mail.Messages.Count == before + 5;
        });
        http.DefaultRequestHeaders.Authorization = null;
    }
}

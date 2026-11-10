using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Support;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Repositories.Implementations;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

/// <summary>
/// Regressions: (1) the same phone written as +84… and 0… must be one identity;
/// (2) self-registration must not derive a colliding/invalid username from the email local part.
/// </summary>
static class RegistrationIdentityChecks
{
    public static async Task Run(SportsCenterDbContext db, HttpClient http, FakeMailGateway mail, long centerId,
        Func<string, Func<Task<bool>>, Task> check)
    {
        const string password = "Identity_Audit42!";

        // Same contract as the web client after the fix: no username is sent.
        async Task<HttpResponseMessage> Register(string email, string? phone = null)
        {
            http.DefaultRequestHeaders.Authorization = null;
            var issued = await http.PostAsJsonAsync("api/auth/request-email-verification", new { email, phone });
            if (issued.StatusCode != HttpStatusCode.OK) return issued;
            return await http.PostAsJsonAsync("api/auth/register",
                new { email, password, fullName = "Identity audit", phone, centerId, emailOtp = mail.Otp });
        }
        Task<User> UserByEmail(string email) => db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        static bool ValidUsername(string username) => Regex.IsMatch(username, "^[a-z0-9._-]{3,100}$");

        await check("phone normalization maps +84 and 84 prefixes to the 0-prefixed national form", () => Task.FromResult(
            PhoneNumberNormalization.Normalize("+84 912-345.678") == "0912345678"
            && PhoneNumberNormalization.Normalize("84912345678") == "0912345678"
            && PhoneNumberNormalization.Normalize("(091) 234 5678") == "0912345678"
            && PhoneNumberNormalization.Normalize("+1 415 555 0100") == "+14155550100"
            && PhoneNumberNormalization.Normalize("   ") is null));

        await check("registration with +84 stores the 0-prefixed phone and blocks the same number written as 0…", async () =>
        {
            var first = await Register("phone.plus84@example.test", "+84 912 345 600");
            if (first.StatusCode != HttpStatusCode.Created) return false;
            var stored = (await UserByEmail("phone.plus84@example.test")).Phone;
            var duplicate = await Register("phone.zero@example.test", "0912345600");
            var body = await duplicate.Content.ReadAsStringAsync();
            return stored == "0912345600" && duplicate.StatusCode == HttpStatusCode.Conflict
                && body.Contains("Số điện thoại") && !await db.Users.AnyAsync(u => u.Email == "phone.zero@example.test");
        });

        await check("login accepts the phone number in any common spelling and keeps failures generic", async () =>
        {
            http.DefaultRequestHeaders.Authorization = null;
            var plus = await http.PostAsJsonAsync("api/auth/login", new { email = "+84 912 345 600", password });
            var dotted = await http.PostAsJsonAsync("api/auth/login", new { email = "0912.345.600", password });
            var wrong = await http.PostAsJsonAsync("api/auth/login", new { email = "0912345600", password = "Wrong_Password42!" });
            var unknown = await http.PostAsJsonAsync("api/auth/login", new { email = "0999999999", password });
            return plus.StatusCode == HttpStatusCode.OK && dotted.StatusCode == HttpStatusCode.OK
                && wrong.StatusCode == HttpStatusCode.Unauthorized && unknown.StatusCode == HttpStatusCode.Unauthorized
                && await wrong.Content.ReadAsStringAsync() == await unknown.Content.ReadAsStringAsync();
        });

        await check("two emails with the same local part on different domains can both register", async () =>
        {
            var gmail = await Register("duy@gmail.example.test");
            var yahoo = await Register("duy@yahoo.example.test");
            if (gmail.StatusCode != HttpStatusCode.Created || yahoo.StatusCode != HttpStatusCode.Created) return false;
            var a = await UserByEmail("duy@gmail.example.test");
            var b = await UserByEmail("duy@yahoo.example.test");
            return a.Username != b.Username && ValidUsername(a.Username) && ValidUsername(b.Username);
        });

        await check("an email with plus-addressing or unusual characters can register", async () =>
        {
            var plus = await Register("duy+gym@example.test");
            var shortLocal = await Register("a@short.example.test");
            return plus.StatusCode == HttpStatusCode.Created && shortLocal.StatusCode == HttpStatusCode.Created
                && ValidUsername((await UserByEmail("duy+gym@example.test")).Username)
                && ValidUsername((await UserByEmail("a@short.example.test")).Username);
        });

        await check("registering an email that is already used returns a Vietnamese email-specific conflict", async () =>
        {
            var again = await Register("duy@gmail.example.test");
            var body = await again.Content.ReadAsStringAsync();
            return again.StatusCode == HttpStatusCode.Conflict && body.Contains("Email") && body.Contains("đã được sử dụng")
                && !body.Contains("Username or email");
        });

        await check("support request priority chosen by the member is stored and returned; invalid values are rejected", async () =>
        {
            var requester = await UserByEmail("duy@gmail.example.test");
            var service = new SupportRequestService(new UnitOfWork(db));
            var urgent = await service.CreateMine(requester.Id, new SubmitSupportRequest("Đèn phòng tập hỏng", "Phòng 2 mất điện", "Urgent"), default);
            var unspecified = await service.CreateMine(requester.Id, new SubmitSupportRequest("Hỏi lịch", "Lịch lớp Yoga", null), default);
            var rejected = false;
            try { await service.CreateMine(requester.Id, new SubmitSupportRequest("Sai", "Sai mức", "Critical"), default); }
            catch (InvalidOperationException) { rejected = true; }
            var stored = await db.SupportRequests.AsNoTracking().SingleAsync(r => r.Id == urgent.Request.Id);
            return urgent.Request.Priority == "Urgent" && stored.Priority == "Urgent"
                && unspecified.Request.Priority == "Normal" && rejected
                && !await db.SupportRequests.AnyAsync(r => r.Subject == "Sai");
        });
    }
}

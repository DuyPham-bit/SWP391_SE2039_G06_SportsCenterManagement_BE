using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Training;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

var passed = 0;
void Pass(string name) { passed++; Console.WriteLine($"PASS: {name}"); }
void Assert(bool value, string name) { if (!value) throw new Exception($"FAIL: {name}"); Pass(name); }
var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
var settings = new Dictionary<string, string?> { ["Jwt:Key"] = secret, ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience" };
var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
var issuer = new JwtTokenIssuer(config);
var token = issuer.Issue(new(12, "test-user", "Member", 42, "user@example.invalid"));
var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
var principal = handler.ValidateToken(token.Token, issuer.ValidationParameters(), out _);
Assert(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) == "12" && principal.FindFirstValue("centerId") == "42", "issued JWT verifies and preserves identity/center");
string Sign(string key, string iss, string aud, DateTime expiry, string algorithm = SecurityAlgorithms.HmacSha256) => handler.WriteToken(
    new JwtSecurityToken(iss, aud, [new Claim(JwtRegisteredClaimNames.Sub, "12")], expires: expiry,
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm)));
foreach (var (name, value) in new[] {
    ("wrong signing key rejected", Sign(new string('x',64), "test-issuer", "test-audience", DateTime.UtcNow.AddHours(1))),
    ("wrong issuer rejected", Sign(secret, "other-issuer", "test-audience", DateTime.UtcNow.AddHours(1))),
    ("wrong audience rejected", Sign(secret, "test-issuer", "other-audience", DateTime.UtcNow.AddHours(1))),
    ("expired JWT rejected", Sign(secret, "test-issuer", "test-audience", DateTime.UtcNow.AddMinutes(-5))),
    ("other signing algorithm rejected", Sign(secret, "test-issuer", "test-audience", DateTime.UtcNow.AddHours(1), SecurityAlgorithms.HmacSha512)),
    ("legacy opaque bearer rejected", "legacy-protected-token") })
{
    try { handler.ValidateToken(value, issuer.ValidationParameters(), out _); throw new Exception($"FAIL: {name}"); }
    catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { Pass(name); }
}
try { await new GoogleTokenValidator(new ConfigurationBuilder().Build()).ValidateAsync("forged", default); throw new Exception("Google config accepted"); }
catch (FlowException ex) when (ex.StatusCode == 503) { Pass("Google without config returns 503"); }
try { await new GeminiService(new HttpClient(), new ConfigurationBuilder().Build()).GenerateAsync("test", [], false, default); throw new Exception("AI config accepted"); }
catch (FlowException ex) when (ex.StatusCode == 503) { Pass("AI without key returns 503 before external request"); }
var plan = new SaveTrainingPlanRequest(1, null, "Plan", "Goal", new(2026,10,11), new(2026,10,10), null, [new("Exercise", "2", "10", null)]);
try { TrainingService.ValidatePlan(plan); throw new Exception("Reversed dates accepted"); }
catch (FlowException ex) when (ex.StatusCode == 400) { Pass("reversed training plan dates rejected"); }

if (!args.Contains("--sql")) { Console.WriteLine($"Passed {passed} checks. Add --sql for isolated SQL Server/HTTP integration."); return; }

// Every run creates its own database; never use the application's configured business database.
var databaseName = "SCMS_ApiIntegration_Test_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
await using var db = new SportsCenterDbContext(new DbContextOptionsBuilder<SportsCenterDbContext>().UseSqlServer(connection).Options);
await DbInitializer.SeedAsync(db, false, true);
Pass("all migrations apply to a new isolated SQL Server database");
var center = new Center { Name = "Test Center", Address = "Test Address", Status = "Active", CreatedAt = DateTime.UtcNow };
var otherCenter = new Center { Name = "Other Test Center", Address = "Other Test Address", Status = "Active", CreatedAt = DateTime.UtcNow };
db.Centers.AddRange(center, otherCenter); await db.SaveChangesAsync();
var sport = new Sport { Name = "Test Sport", Status = "Active", CenterId = center.Id };
db.Sports.Add(sport); await db.SaveChangesAsync();
var roleIds = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);
var password = "Test@" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "a1";
User Account(string name, string role) => new() { Username = name, Email = name + "@example.invalid", PasswordHash = PasswordHashing.Hash(password), RoleId = roleIds[role], Status = "Active", CreatedAt = DateTime.UtcNow };
var coachUser = Account("testcoach", "Coach"); var memberUser = Account("testmember", "Member");
var otherMemberUser = Account("othermember", "Member"); var otherCoachUser = Account("othercoach", "Coach");
var managerUser = Account("testmanager", "Manager"); var candidateUser = Account("candidate", "Member");
var browserCandidateUser = Account("browsercandidate", "Member");
db.Users.AddRange(coachUser, memberUser, otherMemberUser, otherCoachUser, managerUser, candidateUser, browserCandidateUser); await db.SaveChangesAsync();
var coach = new CoachProfile { UserId = coachUser.Id, CenterId = center.Id, FullName = "Test Coach", CoachCode = "TESTCOACH", Status = "Active", CreatedAt = DateTime.UtcNow };
var otherCoach = new CoachProfile { UserId = otherCoachUser.Id, CenterId = otherCenter.Id, FullName = "Other Coach", CoachCode = "OTHERCOACH", Status = "Active", CreatedAt = DateTime.UtcNow };
var member = new MemberProfile { UserId = memberUser.Id, CenterId = center.Id, FullName = "Test Member", MemberCode = "TESTMEMBER", CreatedAt = DateTime.UtcNow };
var otherMember = new MemberProfile { UserId = otherMemberUser.Id, CenterId = otherCenter.Id, FullName = "Other Member", MemberCode = "OTHERMEMBER", CreatedAt = DateTime.UtcNow };
var candidate = new MemberProfile { UserId = candidateUser.Id, CenterId = center.Id, FullName = "Candidate Member", MemberCode = "CANDIDATE", CreatedAt = DateTime.UtcNow };
var browserCandidate = new MemberProfile { UserId = browserCandidateUser.Id, CenterId = center.Id,
    FullName = "Browser Candidate", MemberCode = "BROWSERCANDIDATE", CreatedAt = DateTime.UtcNow };
db.CoachProfiles.AddRange(coach, otherCoach); db.MemberProfiles.AddRange(member, otherMember, candidate, browserCandidate); await db.SaveChangesAsync();
// StaffManagement creates both profiles for a coach; a staff row must not grant manager visibility.
db.StaffProfiles.Add(new StaffProfile { UserId = coachUser.Id, CenterId = center.Id,
    StaffCode = "TESTCOACHSTAFF", FullName = coach.FullName, Position = "Coach", Status = "Active", CreatedAt = DateTime.UtcNow });
await db.SaveChangesAsync();
db.StaffProfiles.Add(new StaffProfile { UserId = managerUser.Id, CenterId = center.Id, StaffCode = "TESTMANAGER",
    FullName = "Test Manager", Position = "Manager", Status = "Active", CreatedAt = DateTime.UtcNow });
var dayStartUtc = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
db.CenterCheckins.AddRange(Enumerable.Range(0,101).Select(_ => new CenterCheckin { CenterId = center.Id,
    MemberId = member.Id, CheckedInBy = managerUser.Id, CheckInTime = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }));
db.CenterCheckins.Add(new CenterCheckin { CenterId = center.Id, MemberId = member.Id,
    CheckInTime = dayStartUtc.AddMinutes(-1), CreatedAt = DateTime.UtcNow });
await db.SaveChangesAsync();
var cls = new ClassEntity { CenterId = center.Id, SportId = sport.Id, Name = "Assigned", Capacity = 10, DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow };
var unassigned = new ClassEntity { CenterId = center.Id, SportId = sport.Id, Name = "Unassigned", Capacity = 10, DurationMinutes = 60, Status = "Published", CreatedAt = DateTime.UtcNow };
db.Classes.AddRange(cls, unassigned); await db.SaveChangesAsync();
db.ClassCoaches.Add(new ClassCoach { ClassId = cls.Id, CoachId = coach.Id, IsPrimary = true });
db.ClassEnrollments.Add(new ClassEnrollment { ClassId = cls.Id, MemberId = member.Id, Status = "Confirmed", RegisteredAt = DateTime.UtcNow });
await db.SaveChangesAsync();

var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var apiDll = typeof(JwtTokenIssuer).Assembly.Location;
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(apiDll)! };
start.ArgumentList.Add(apiDll); start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
start.Environment["ConnectionStrings__SportsCenter"] = connection;
start.Environment["Database__BaselineLegacySchema"] = "false";
start.Environment["Jwt__Key"] = secret; start.Environment["Jwt__Issuer"] = "test-issuer"; start.Environment["Jwt__Audience"] = "test-audience";
start.Environment["Bootstrap__SystemAdmin__Username"] = "testsystemadmin";
start.Environment["Bootstrap__SystemAdmin__Email"] = "testsystemadmin@example.invalid";
start.Environment["Bootstrap__SystemAdmin__Password"] = password;
start.Environment["AI__ApiKey"] = ""; start.Environment["Google__ClientId"] = "";
start.Environment["Cors__AllowedOrigins__0"] = "http://localhost:3004";
using var process = Process.Start(start)!;
var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/api/"), Timeout = TimeSpan.FromSeconds(20) };
    var ready = false;
    for (var attempt = 0; attempt < 60; attempt++)
    {
        if (process.HasExited) throw new Exception("API startup failed: " + await stderr + await stdout);
        try { using var health = await client.GetAsync("health"); if (health.IsSuccessStatusCode) { ready = true; break; } }
        catch (HttpRequestException) { }
        await Task.Delay(500);
    }
    Assert(ready, "HTTP health endpoint is public");
    async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected, string name)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expected) throw new Exception($"FAIL: {name}: {response.StatusCode}: {body}");
        Pass(name); return string.IsNullOrWhiteSpace(body) ? default : JsonDocument.Parse(body).RootElement.Clone();
    }
    async Task<string> Login(User user) => (await Json(await client.PostAsJsonAsync("auth/login", new { email = user.Email, password }), HttpStatusCode.OK, $"password login {user.Username}")).GetProperty("accessToken").GetString()!;
    await Json(await client.GetAsync("profile/me"), HttpStatusCode.Unauthorized, "protected API rejects missing JWT");
    var coachToken = await Login(coachUser); var memberToken = await Login(memberUser); var otherToken = await Login(otherMemberUser);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coachToken);
    var classes = await Json(await client.GetAsync("coaches/me/classes"), HttpStatusCode.OK, "coach catalog and relational roster load");
    Assert(classes.GetArrayLength() == 1 && classes[0].GetProperty("members").GetArrayLength() == 1, "coach sees only assigned class and members");
    var request = new { classId = cls.Id, memberId = (long?)null, title = "Shared Plan", goal = "General fitness", startDate = "2026-10-11", endDate = (string?)null,
        fitnessCondition = (string?)null, exercises = new[] { new { name = "Warmup", sets = "2", reps = "10 min", note = "Easy" } } };
    var saved = await Json(await client.PostAsJsonAsync("coaches/me/training-plans", request), HttpStatusCode.OK, "class training plan persists");
    var planId = saved.GetProperty("id").GetInt64();
    await Json(await client.PostAsJsonAsync("coaches/me/training-plans", request with { classId = unassigned.Id }), HttpStatusCode.Forbidden, "coach cannot write unassigned class");
    await Json(await client.PutAsJsonAsync("coaches/me/personal-training-plans", request with { memberId = otherMember.Id }), HttpStatusCode.Forbidden, "coach cannot write unrelated member");
    await Json(await client.PutAsJsonAsync("coaches/me/personal-training-plans", request with { memberId = member.Id }), HttpStatusCode.OK, "personal training plan persists");
    await Json(await client.PutAsJsonAsync("coaches/me/personal-training-plans", request with { memberId = member.Id, title = "Updated Personal" }), HttpStatusCode.OK, "personal plan updates existing row");
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = member.Id, weight = 70, bodyFat = 20, muscleMass = 30, workoutResult = "Completed", coachFeedback = "Good" }), HttpStatusCode.OK, "progress persists all requested measurements");
    await Json(await client.PostAsJsonAsync("coaches/me/notifications", new { classId = cls.Id, title = "Homework", content = "Practice" }), HttpStatusCode.NoContent, "coach notification and recipients persist");
    var sent = await Json(await client.GetAsync("coaches/me/notifications"), HttpStatusCode.OK, "coach can read own sent notifications");
    var notificationId = sent[0].GetProperty("id").GetInt64();
    await Json(await client.GetAsync("coaches/me/training-plans"), HttpStatusCode.OK, "coach reads stored training plans");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
    var plans = await Json(await client.GetAsync("members/me/training-plans"), HttpStatusCode.OK, "member reads own and shared plans");
    Assert(plans.GetArrayLength() == 2 && plans.EnumerateArray().Any(p => p.GetProperty("title").GetString() == "Updated Personal"), "personal plan upsert does not duplicate active plan");
    var progress = await Json(await client.GetAsync("members/me/progress"), HttpStatusCode.OK, "member reads stored progress");
    Assert(progress[0].GetProperty("muscleMass").GetDecimal() == 30 && progress[0].GetProperty("coachFeedback").GetString() == "Good", "progress contract roundtrips metrics and feedback");
    await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "member receives coach notification");
    await Json(await client.PutAsync($"notifications/{notificationId}/read", null), HttpStatusCode.NoContent, "notification owner marks read");
    await Json(await client.PostAsJsonAsync("ai/chat", new { message = "Warmup?" }), HttpStatusCode.ServiceUnavailable, "AI without configured key returns truthful 503");
    await Json(await client.PostAsJsonAsync("ai/chat", new { message = "Warmup?", conversationId = 99999 }), HttpStatusCode.NotFound, "AI rejects unknown or foreign conversation before provider call");
    await Json(await client.PostAsJsonAsync("coaches/me/training-plans", request), HttpStatusCode.Forbidden, "member cannot write coach plans");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
    var otherPlans = await Json(await client.GetAsync("members/me/training-plans"), HttpStatusCode.OK, "other member plan query succeeds without leaking plans");
    Assert(otherPlans.GetArrayLength() == 0, "unrelated member cannot see shared or personal plans");
    await Json(await client.PutAsync($"notifications/{notificationId}/read", null), HttpStatusCode.NotFound, "other member cannot mark foreign notification");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coachToken);
    await Json(await client.DeleteAsync($"coaches/me/training-plans/{planId}"), HttpStatusCode.NoContent, "coach archives own plan");
    await db.Users.Where(u => u.Id == coachUser.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Disabled"));
    await Json(await client.GetAsync("profile/me"), HttpStatusCode.Unauthorized, "account disabled after login immediately invalidates JWT");
    client.DefaultRequestHeaders.Authorization = null;
    await Json(await client.PostAsJsonAsync("auth/google", new { idToken = "forged", centerId = center.Id }), HttpStatusCode.ServiceUnavailable, "Google without configured client ID returns truthful 503");
    using var preflight = new HttpRequestMessage(HttpMethod.Options, "profile/me");
    preflight.Headers.Add("Origin", "http://localhost:3004"); preflight.Headers.Add("Access-Control-Request-Method", "GET");
    preflight.Headers.Add("Access-Control-Request-Headers", "authorization");
    using var cors = await client.SendAsync(preflight);
    Assert(cors.StatusCode == HttpStatusCode.NoContent && cors.Headers.GetValues("Access-Control-Allow-Origin").Single() == "http://localhost:3004", "CORS preflight allows configured frontend and Authorization header");
    using var swaggerClient = new HttpClient();
    var swagger = await swaggerClient.GetFromJsonAsync<JsonElement>($"http://127.0.0.1:{port}/swagger/v1/swagger.json");
    Assert(swagger.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString() == "bearer", "Swagger advertises JWT bearer authentication");
    var managerToken = await Login(managerUser);
    var candidateToken = await Login(candidateUser);
    var appointment = new { memberId = candidate.Id, roleName = "Coach", hireDate = "2026-10-11", specialization = "Swimming" };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
    await Json(await client.PostAsJsonAsync($"centers/{center.Id}/staff/appointments", appointment), HttpStatusCode.Forbidden, "member cannot appoint staff");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    await Json(await client.PostAsJsonAsync($"centers/{otherCenter.Id}/staff/appointments", appointment), HttpStatusCode.Forbidden, "manager cannot appoint in foreign center");
    await Json(await client.PostAsJsonAsync($"centers/{center.Id}/staff/appointments", appointment with { memberId = otherMember.Id }), HttpStatusCode.NotFound, "manager cannot appoint foreign member");
    await Json(await client.PostAsJsonAsync($"centers/{center.Id}/staff/appointments", appointment with { roleName = "Manager" }), HttpStatusCode.BadRequest, "appointment cannot grant manager role");
    var appointed = await Json(await client.PostAsJsonAsync($"centers/{center.Id}/staff/appointments", appointment), HttpStatusCode.Created, "existing member appointment succeeds");
    Assert(appointed.GetProperty("userId").GetInt64() == candidateUser.Id &&
        await db.MemberProfiles.AnyAsync(m => m.Id == candidate.Id && m.UserId == candidateUser.Id) &&
        await db.Users.Where(u => u.Id == candidateUser.Id).Select(u => u.PasswordHash).SingleAsync() == candidateUser.PasswordHash,
        "appointment preserves user ID, member history and password");
    await Json(await client.PostAsJsonAsync($"centers/{center.Id}/staff/appointments", appointment), HttpStatusCode.Conflict, "duplicate appointment rejected");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);
    await Json(await client.GetAsync("coaches/me/classes"), HttpStatusCode.OK, "existing JWT immediately uses new database role");
    await Json(await client.GetAsync("members/me/progress"), HttpStatusCode.Forbidden, "previous role cannot access member API after appointment");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    var staffUpdate = new { fullName = candidate.FullName, status = "Active", roleName = "Receptionist" };
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/staff/{candidateUser.Id}", staffUpdate), HttpStatusCode.OK, "unassigned coach can become receptionist");
    Assert(await db.CoachProfiles.AnyAsync(c => c.UserId == candidateUser.Id && c.Status == "Inactive"), "coach profile disabled while historical rows remain");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/staff/{candidateUser.Id}", staffUpdate with { roleName = "Coach" }), HttpStatusCode.OK, "receptionist can restore coach role");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/staff/{candidateUser.Id}", staffUpdate with { roleName = "Manager" }), HttpStatusCode.Forbidden, "staff role update cannot elevate to manager");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/staff/{coachUser.Id}", staffUpdate), HttpStatusCode.Conflict, "assigned coach role cannot change before removing assignments");
    var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd");
    var checkins = await Json(await client.GetAsync($"reports/checkins?from={today}&to={today}&centerId={otherCenter.Id}"), HttpStatusCode.OK, "manager check-in report uses assigned center");
    Assert(checkins.GetProperty("count").GetInt32() == 101 && checkins.GetProperty("centerId").GetInt64() == center.Id,
        "check-in count is not truncated and respects Vietnam day boundary");
    await Json(await client.GetAsync("reports/checkins?from=2026-10-11&to=2026-10-10"), HttpStatusCode.BadRequest, "check-in report rejects reversed range");
    if (args.Contains("--browser"))
    {
        await db.Users.Where(u => u.Id == coachUser.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Active"));
        var browserStart = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        browserStart.ArgumentList.Add(Path.Combine(Directory.GetCurrentDirectory(), "Frontend/tests/browser-smoke.cjs"));
        browserStart.Environment["SCMS_TEST_API"] = $"http://127.0.0.1:{port}";
        browserStart.Environment["SCMS_TEST_COACH_EMAIL"] = coachUser.Email;
        browserStart.Environment["SCMS_TEST_MEMBER_EMAIL"] = memberUser.Email;
        browserStart.Environment["SCMS_TEST_MANAGER_EMAIL"] = managerUser.Email;
        browserStart.Environment["SCMS_TEST_PASSWORD"] = password;
        using var browserProcess = Process.Start(browserStart)!;
        var browserOutput = browserProcess.StandardOutput.ReadToEndAsync();
        var browserErrors = browserProcess.StandardError.ReadToEndAsync();
        await browserProcess.WaitForExitAsync();
        Console.WriteLine(await browserOutput);
        Console.WriteLine(await browserErrors);
        Assert(browserProcess.ExitCode == 0, "browser workflow checks pass against the isolated API");
    }
    Console.WriteLine($"Passed {passed} checks. Isolated test database: {databaseName}");
}
finally
{
    if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
    var reportDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".validation");
    Directory.CreateDirectory(reportDirectory);
    await File.WriteAllTextAsync(Path.Combine(reportDirectory, "api-integration-runtime.log"), await stdout + await stderr);
    // Retain the isolated database for inspection; never delete a database from a computed connection string.
}

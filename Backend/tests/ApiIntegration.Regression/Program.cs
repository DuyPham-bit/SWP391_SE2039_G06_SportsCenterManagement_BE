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
var member = new MemberProfile { UserId = memberUser.Id, CenterId = center.Id, FullName = "Test Member", MemberCode = "TESTMEMBER", CreatedAt = DateTime.UtcNow,
    FitnessGoal = "Build endurance", FitnessLevel = "Beginner", MedicalNote = "Avoid high impact", HeightCm = 170, WeightKg = 70, DateOfBirth = new(2000, 1, 1) };
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

var memberToday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
var studio = new Room { CenterId = center.Id, Name = "Member Studio", Capacity = 20, Status = "Active", CreatedAt = DateTime.UtcNow };
var newStudio = new Room { CenterId = center.Id, Name = "Updated Studio", Capacity = 20, Status = "Active", CreatedAt = DateTime.UtcNow };
db.Rooms.AddRange(studio, newStudio); await db.SaveChangesAsync();
cls.RoomId = studio.Id; coach.Specialization = sport.Name;
var memberSchedule = new ClassSchedule { ClassId = cls.Id, DayOfWeek = (int)memberToday.AddDays(1).DayOfWeek,
    StartDate = memberToday.AddDays(1), EndDate = memberToday.AddDays(1), StartTime = new(10, 0), EndTime = new(11, 0), Status = "Active" };
var cancelledSchedule = new ClassSchedule { ClassId = cls.Id, DayOfWeek = (int)memberToday.AddDays(1).DayOfWeek,
    StartDate = memberToday.AddDays(1), EndDate = memberToday.AddDays(1), StartTime = new(14, 0), EndTime = new(15, 0), Status = "Active" };
db.ClassSchedules.AddRange(memberSchedule, cancelledSchedule); await db.SaveChangesAsync();
ClassSession MemberSession(DateOnly date, int hour, long? scheduleId = null) => new() { ClassId = cls.Id, CoachId = coach.Id,
    RoomId = studio.Id, SessionDate = date, StartTime = new(hour, 0), EndTime = new(hour + 1, 0), ScheduleId = scheduleId,
    SessionStatus = "Scheduled", CreatedAt = DateTime.UtcNow };
var attendedSession = MemberSession(memberToday.AddDays(-1), 9);
attendedSession.SessionStatus = "Completed";
var upcomingSession = MemberSession(memberToday.AddDays(1), 10, memberSchedule.Id);
var cancelledSession = MemberSession(memberToday.AddDays(1), 14, cancelledSchedule.Id);
db.ClassSessions.AddRange(attendedSession, upcomingSession, cancelledSession); await db.SaveChangesAsync();
foreach (var session in new[] { attendedSession, upcomingSession, cancelledSession })
    db.SessionBookings.Add(new SessionBooking { SessionId = session.Id, MemberId = member.Id, Status = "Booked", BookedAt = DateTime.UtcNow });
db.Attendances.Add(new Attendance { SessionId = attendedSession.Id, MemberId = member.Id, AttendanceStatus = "PRESENT",
    Note = "Member attendance note", CheckInTime = DateTime.UtcNow.AddDays(-1), CheckedInBy = coachUser.Id, CreatedAt = DateTime.UtcNow });
await db.SaveChangesAsync();

for (var index = 0; index < 22; index++)
{
    var paginationUser = Account($"paginationmember{index}", "Member");
    db.Users.Add(paginationUser); await db.SaveChangesAsync();
    db.MemberProfiles.Add(new MemberProfile { UserId = paginationUser.Id, CenterId = center.Id,
        FullName = $"Pagination Member {index:D2}", MemberCode = $"PAGEMEMBER{index}", CreatedAt = DateTime.UtcNow });
}
db.AuditLogs.AddRange(Enumerable.Range(0, 65).Select(index => new AuditLog { CenterId = center.Id,
    UserId = managerUser.Id, Action = "MANAGER.PAGE.TEST", EntityType = "Center", EntityId = center.Id,
    CreatedAt = DateTime.UtcNow.AddDays(-1).AddSeconds(index) }));
var sharedSport = new Sport { Name = "Shared Manager Test Sport", Status = "Active" };
db.Sports.Add(sharedSport); await db.SaveChangesAsync();

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
    async Task<string> Login(User user)
    {
        using var response = await client.PostAsJsonAsync("auth/login", new { email = user.Email, password });
        if (response.Headers.Contains("Set-Cookie")) throw new Exception("JWT login must not create an authentication Session cookie.");
        return (await Json(response, HttpStatusCode.OK, $"password login {user.Username}")).GetProperty("accessToken").GetString()!;
    }
    await Json(await client.GetAsync("profile/me"), HttpStatusCode.Unauthorized, "protected API rejects missing JWT");
    var coachToken = await Login(coachUser); var memberToken = await Login(memberUser); var otherToken = await Login(otherMemberUser);
    using (var bearerOnlyClient = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = client.BaseAddress })
    {
        bearerOnlyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coachToken);
        var jwtProfile = await Json(await bearerOnlyClient.GetAsync("profile/me"), HttpStatusCode.OK, "fresh client authenticates with JWT alone and no cookies");
        Assert(jwtProfile.GetProperty("userId").GetInt64() == coachUser.Id, "JWT alone preserves the authenticated account");
    }
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coachToken);
    var classes = await Json(await client.GetAsync("coaches/me/classes"), HttpStatusCode.OK, "coach catalog and relational roster load");
    Assert(classes.GetArrayLength() == 1 && classes[0].GetProperty("members").GetArrayLength() == 1, "coach sees only assigned class and members");
    var roster = await Json(await client.GetAsync($"coaches/me/classes/{cls.Id}/members"), HttpStatusCode.OK, "coach reads basic member profile");
    Assert(roster[0].GetProperty("fitnessGoal").GetString() == "Build endurance" && roster[0].GetProperty("medicalNote").GetString() == "Avoid high impact"
        && roster[0].GetProperty("heightCm").GetDecimal() == 170, "coach profile includes stored goals, level and physical condition");
    await Json(await client.GetAsync($"coaches/me/classes/{unassigned.Id}/members"), HttpStatusCode.Forbidden, "coach cannot inspect unassigned roster");
    var teaching = await Json(await client.GetAsync($"coaches/me/teaching-schedule?from={memberToday.AddDays(-2):yyyy-MM-dd}&to={memberToday.AddDays(2):yyyy-MM-dd}&includeHistory=true"), HttpStatusCode.OK, "coach loads historical teaching dates");
    Assert(teaching.GetArrayLength() == 3 && teaching[0].GetProperty("roomName").GetString() == studio.Name, "teaching schedule contains real dates and room names");
    Assert(teaching[0].GetProperty("sessionStatus").GetString() == "Completed", "coach can see completed sessions when history is requested");
    var legacyTeaching = await Json(await client.GetAsync($"coaches/me/teaching-schedule?from={memberToday.AddDays(-2):yyyy-MM-dd}&to={memberToday.AddDays(2):yyyy-MM-dd}"), HttpStatusCode.OK, "default teaching query remains compatible");
    Assert(legacyTeaching.GetArrayLength() == 2, "default teaching query still excludes completed sessions");
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = member.Id, sessionId = attendedSession.Id, workoutResult = "Session endurance result", coachFeedback = "Steady breathing" }), HttpStatusCode.OK, "session progress persists without fabricated body metrics");
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = member.Id, sessionId = upcomingSession.Id, workoutResult = "Future" }), HttpStatusCode.Conflict, "progress rejects unfinished sessions");
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = unassigned.Id, memberId = member.Id, sessionId = attendedSession.Id, workoutResult = "Unauthorized" }), HttpStatusCode.Forbidden, "progress rejects unassigned classes");
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = member.Id, sessionId = 999999L, workoutResult = "Unknown" }), HttpStatusCode.BadRequest, "progress rejects unknown session IDs");
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = otherMember.Id, sessionId = attendedSession.Id, workoutResult = "Unrelated" }), HttpStatusCode.Forbidden, "progress rejects unrelated members");
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = member.Id, sessionId = attendedSession.Id, date = memberToday.ToString("yyyy-MM-dd"), workoutResult = "Wrong day" }), HttpStatusCode.BadRequest, "progress rejects dates that differ from the chosen session");
    await db.ClassSessions.Where(s => s.Id == attendedSession.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.SessionStatus, "Cancelled"));
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = member.Id, sessionId = attendedSession.Id, workoutResult = "Cancelled" }), HttpStatusCode.Conflict, "progress rejects cancelled sessions");
    await db.ClassSessions.Where(s => s.Id == attendedSession.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.SessionStatus, "Completed"));
    await db.SessionBookings.Where(b => b.SessionId == attendedSession.Id && b.MemberId == member.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "Cancelled"));
    await Json(await client.PostAsJsonAsync("coaches/me/progress", new { classId = cls.Id, memberId = member.Id, sessionId = attendedSession.Id, workoutResult = "Not booked" }), HttpStatusCode.BadRequest, "progress requires a valid booking even when member is enrolled in the class");
    await db.SessionBookings.Where(b => b.SessionId == attendedSession.Id && b.MemberId == member.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, "Booked"));
    var sessionProgress = await Json(await client.GetAsync("coaches/me/progress"), HttpStatusCode.OK, "coach reloads session progress");
    Assert(sessionProgress[0].GetProperty("sessionId").GetInt64() == attendedSession.Id
        && sessionProgress[0].GetProperty("weight").ValueKind == JsonValueKind.Null
        && sessionProgress[0].GetProperty("date").GetString() == attendedSession.SessionDate.ToString("yyyy-MM-dd"), "session progress roundtrips ID, date and empty measurements");
    var aiContext = await Json(await client.GetAsync($"coaches/me/classes/{cls.Id}/members/{member.Id}/ai-context"), HttpStatusCode.OK, "AI context loads stored profile and history");
    Assert(aiContext.GetProperty("recentHistory")[0].GetProperty("workoutResult").GetString() == "Session endurance result"
        && aiContext.GetProperty("fitnessGoal").GetString() == "Build endurance" && !aiContext.TryGetProperty("memberName", out _)
        && !aiContext.TryGetProperty("email", out _), "AI context uses actual member history without identity fields");
    var contextFixtures = Enumerable.Range(1, 12).Select(i => new TrainingResult { MemberId = member.Id, CoachId = coach.Id,
        ClassId = cls.Id, ResultDate = memberToday.AddDays(-i), WorkoutResult = $"Context history {i}", CreatedAt = DateTime.UtcNow.AddMinutes(-i) }).ToList();
    contextFixtures.Add(new TrainingResult { MemberId = member.Id, CoachId = otherCoach.Id, ClassId = unassigned.Id,
        ResultDate = memberToday, WorkoutResult = "Other class private history", CreatedAt = DateTime.UtcNow });
    db.TrainingResults.AddRange(contextFixtures); await db.SaveChangesAsync();
    var boundedContext = await new TrainingService(db).GetRecommendationContextAsync(coachUser.Id, cls.Id, member.Id, default);
    Assert(boundedContext.RecentHistory.Length == 10 && boundedContext.RecentHistory.All(h => h.WorkoutResult != "Other class private history")
        && boundedContext.RecentHistory[0].Date >= boundedContext.RecentHistory[^1].Date, "AI history is bounded, ordered and restricted to the selected class");
    db.TrainingResults.RemoveRange(contextFixtures); await db.SaveChangesAsync();
    await Json(await client.GetAsync($"coaches/me/classes/{cls.Id}/members/{otherMember.Id}/ai-context"), HttpStatusCode.Forbidden, "AI context rejects unrelated member");
    await Json(await client.GetAsync($"coaches/me/classes/{unassigned.Id}/members/{member.Id}/ai-context"), HttpStatusCode.Forbidden, "AI context rejects unassigned class");
    await Json(await client.PostAsJsonAsync("ai/recommendations", new { classId = cls.Id, memberId = member.Id, fitnessGoal = "Endurance", currentLevel = "Beginner" }), HttpStatusCode.ServiceUnavailable, "coach AI without key returns truthful 503");
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
    var memberBookings = await Json(await client.GetAsync("members/me/session-bookings"), HttpStatusCode.OK, "member loads personal schedule and attendance");
    var attendedBooking = memberBookings.EnumerateArray().Single(b => b.GetProperty("sessionId").GetInt64() == attendedSession.Id);
    Assert(memberBookings.GetArrayLength() == 3 && attendedBooking.GetProperty("attendanceStatus").GetString() == "PRESENT"
        && attendedBooking.GetProperty("attendanceNote").GetString() == "Member attendance note"
        && attendedBooking.GetProperty("roomName").GetString() == studio.Name
        && attendedBooking.GetProperty("coachName").GetString() == coach.FullName
        && attendedBooking.GetProperty("coachSpecialization").GetString() == sport.Name,
        "personal schedule joins real room, coach and member-owned attendance");
    Assert(memberBookings.EnumerateArray().Single(b => b.GetProperty("sessionId").GetInt64() == upcomingSession.Id)
        .GetProperty("attendanceStatus").ValueKind == JsonValueKind.Null, "future booking does not fabricate attendance");
    var memberMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "member notifications load");
    Assert(memberMessages.EnumerateArray().Any(n => n.GetProperty("id").GetInt64() == notificationId
        && !n.GetProperty("isRead").GetBoolean()), "member receives unread coach message with recipient link");
    Assert(memberMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == "TrainingPlanUpdated") == 3,
        "shared, personal and updated training plans notify the member");
    Assert(memberMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == "ProgressUpdated") == 2,
        "recorded workout results notify the member");
    await Json(await client.PutAsync($"notifications/{notificationId}/read", null), HttpStatusCode.NoContent, "notification owner marks read");
    await Json(await client.PostAsJsonAsync("ai/chat", new { message = "Warmup?" }), HttpStatusCode.ServiceUnavailable, "AI without configured key returns truthful 503");
    await Json(await client.PostAsJsonAsync("ai/chat", new { message = "Warmup?", conversationId = 99999 }), HttpStatusCode.NotFound, "AI rejects unknown or foreign conversation before provider call");
    await Json(await client.PostAsJsonAsync("coaches/me/training-plans", request), HttpStatusCode.Forbidden, "member cannot write coach plans");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
    var otherPlans = await Json(await client.GetAsync("members/me/training-plans"), HttpStatusCode.OK, "other member plan query succeeds without leaking plans");
    Assert(otherPlans.GetArrayLength() == 0, "unrelated member cannot see shared or personal plans");
    var unrelatedBookings = await Json(await client.GetAsync("members/me/session-bookings"), HttpStatusCode.OK, "unrelated member personal schedule loads");
    Assert(unrelatedBookings.GetArrayLength() == 0, "personal schedule never leaks another member attendance or bookings");
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
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    var membersPage1 = await Json(await client.GetAsync($"centers/{center.Id}/members?page=1&pageSize=20"), HttpStatusCode.OK, "manager can list members without a query");
    var membersPage2 = await Json(await client.GetAsync($"centers/{center.Id}/members?page=2&pageSize=20"), HttpStatusCode.OK, "manager can read next member page");
    Assert(membersPage1.GetProperty("totalCount").GetInt32() == await db.MemberProfiles.CountAsync(m => m.CenterId == center.Id)
        && !membersPage1.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("memberId").GetInt64())
            .Intersect(membersPage2.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("memberId").GetInt64())).Any(),
        "member pagination counts all center profiles without duplicates");
    await Json(await client.GetAsync($"centers/{otherCenter.Id}/members"), HttpStatusCode.Forbidden, "manager cannot list foreign-center members");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/members/{otherMember.Id}/status", new { status = "Disabled" }), HttpStatusCode.NotFound, "manager cannot lock foreign member");
    var editedMember = await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/members/{member.Id}", new { fullName = member.FullName, address = "Manager updated address" }), HttpStatusCode.OK, "manager can edit member profile");
    Assert(editedMember.GetProperty("address").GetString() == "Manager updated address", "member update persists requested profile data");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/members/{member.Id}/status", new { status = "Disabled" }), HttpStatusCode.OK, "manager locks member account");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
    await Json(await client.GetAsync("members/me"), HttpStatusCode.Unauthorized, "locked member JWT stops accessing protected data");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/members/{member.Id}/status", new { status = "Active" }), HttpStatusCode.OK, "manager restores member account");
    var sportBody = new { name = "Manager New Sport", description = "Manager sport description", status = "Active" };
    var createdSport = await Json(await client.PostAsJsonAsync($"centers/{center.Id}/sports", sportBody), HttpStatusCode.Created, "manager creates center sport");
    var managedSportId = createdSport.GetProperty("id").GetInt64();
    await Json(await client.PostAsJsonAsync($"centers/{center.Id}/sports", sportBody), HttpStatusCode.Conflict, "duplicate center sport rejected");
    await Json(await client.PutAsJsonAsync($"centers/{center.Id}/sports/{managedSportId}", sportBody with { status = "Inactive" }), HttpStatusCode.OK, "unused sport can be deactivated");
    var managementSports = await Json(await client.GetAsync($"centers/{center.Id}/sports/all"), HttpStatusCode.OK, "manager catalog includes inactive sports");
    Assert(managementSports.EnumerateArray().Any(s => s.GetProperty("id").GetInt64() == managedSportId && s.GetProperty("status").GetString() == "Inactive"), "inactive sport remains manageable");
    await Json(await client.PutAsJsonAsync($"centers/{center.Id}/sports/{sport.Id}", sportBody with { name = sport.Name, status = "Inactive" }), HttpStatusCode.Conflict, "sport with open classes cannot be deactivated");
    await Json(await client.PutAsJsonAsync($"centers/{center.Id}/sports/{sharedSport.Id}", sportBody), HttpStatusCode.NotFound, "manager cannot edit shared sport");
    await Json(await client.PostAsJsonAsync($"centers/{otherCenter.Id}/sports", sportBody), HttpStatusCode.Forbidden, "manager cannot create sport in foreign center");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
    await Json(await client.PostAsJsonAsync($"centers/{center.Id}/sports", sportBody), HttpStatusCode.Forbidden, "member cannot create sports");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    var auditPage2 = await Json(await client.GetAsync($"centers/{center.Id}/audit-logs?page=2&pageSize=50"), HttpStatusCode.OK, "manager reads audit records beyond first 50");
    Assert(auditPage2.GetProperty("totalCount").GetInt32() > 50 && auditPage2.GetProperty("items").GetArrayLength() > 0
        && auditPage2.GetProperty("items").EnumerateArray().All(log => log.GetProperty("centerId").GetInt64() == center.Id), "audit pagination stays within assigned center");
    var reportToday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    var membershipReport = await Json(await client.GetAsync($"reports/membership?from={reportToday:yyyy-MM-dd}&to={reportToday:yyyy-MM-dd}&groupBy=month"), HttpStatusCode.OK, "manager requests membership report by month");
    Assert(membershipReport.GetProperty("totalMembers").GetInt32() == await db.MemberProfiles.CountAsync(m => m.CenterId == center.Id)
        && membershipReport.GetProperty("newMembers").GetInt32() >= 22, "membership report includes new profiles without subscriptions");
    var classCatalog = await Json(await client.GetAsync($"centers/{center.Id}/classes/all"), HttpStatusCode.OK, "manager class catalog includes enrollment counts");
    Assert(classCatalog.EnumerateArray().Single(c => c.GetProperty("id").GetInt64() == cls.Id).GetProperty("enrolledCount").GetInt32() == 1, "coach assignment displays real confirmed enrollment count");
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
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateToken);
    await Json(await client.GetAsync($"centers/{center.Id}/members"), HttpStatusCode.Forbidden, "receptionist still needs search query to access members");
    await Json(await client.GetAsync($"centers/{center.Id}/members?query=Test"), HttpStatusCode.OK, "receptionist retains existing member search");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    Assert(await db.CoachProfiles.AnyAsync(c => c.UserId == candidateUser.Id && c.Status == "Inactive"), "coach profile disabled while historical rows remain");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/staff/{candidateUser.Id}", staffUpdate with { roleName = "Coach" }), HttpStatusCode.OK, "receptionist can restore coach role");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/staff/{candidateUser.Id}", staffUpdate with { roleName = "Manager" }), HttpStatusCode.Forbidden, "staff role update cannot elevate to manager");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/staff/{coachUser.Id}", staffUpdate), HttpStatusCode.Conflict, "assigned coach role cannot change before removing assignments");
    var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd");
    var checkins = await Json(await client.GetAsync($"reports/checkins?from={today}&to={today}&centerId={otherCenter.Id}"), HttpStatusCode.OK, "manager check-in report uses assigned center");
    Assert(checkins.GetProperty("count").GetInt32() == 101 && checkins.GetProperty("centerId").GetInt64() == center.Id,
        "check-in count is not truncated and respects Vietnam day boundary");
    await Json(await client.GetAsync("reports/checkins?from=2026-10-11&to=2026-10-10"), HttpStatusCode.BadRequest, "check-in report rejects reversed range");
    await Json(await client.PutAsJsonAsync($"classes/{cls.Id}", new { sportId = sport.Id, roomId = newStudio.Id,
        name = cls.Name, description = (string?)null, level = (string?)null, capacity = 10, durationMinutes = 60, allowWaitlist = true }),
        HttpStatusCode.OK, "manager changes classroom for upcoming sessions");
    await Json(await client.DeleteAsync($"classes/{cls.Id}/schedules/{cancelledSchedule.Id}"), HttpStatusCode.OK, "manager cancels a booked schedule");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
    var scheduleMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "member receives persisted schedule-change notifications");
    Assert(scheduleMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == "ScheduleChanged") == 2,
        "room change and cancellation each notify the affected member once");
    var updatedBookings = await Json(await client.GetAsync("members/me/session-bookings"), HttpStatusCode.OK, "member schedule refresh reflects room change and cancellation");
    Assert(updatedBookings.EnumerateArray().Single(b => b.GetProperty("sessionId").GetInt64() == cancelledSession.Id).GetProperty("sessionStatus").GetString() == "Cancelled"
        && updatedBookings.EnumerateArray().Single(b => b.GetProperty("sessionId").GetInt64() == upcomingSession.Id).GetProperty("roomName").GetString() == newStudio.Name,
        "member sees the same committed schedule state as the notification");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
    var unrelatedMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "unrelated member notifications load");
    Assert(unrelatedMessages.GetArrayLength() == 0, "schedule-change notifications only reach affected members");
    await db.Users.Where(u => u.Id == coachUser.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Active"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    await Json(await client.PostAsJsonAsync($"classes/{cls.Id}/coaches", new { coachId = coach.Id, isPrimary = true }), HttpStatusCode.OK, "manager assigns primary coach");
    Assert(await db.AuditLogs.AnyAsync(log => log.Action == "CLASS_COACH.ASSIGNED" && log.UserId == managerUser.Id
        && log.CenterId == center.Id && log.EntityId == cls.Id && log.OldValues != null && log.NewValues != null),
        "coach assignment and actor audit persist together");
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
    // Exercise additional notification producers after browser checks so their fixtures stay stable.
    var waitingUser = Account("notificationwaiting", "Member");
    db.Users.Add(waitingUser); await db.SaveChangesAsync();
    var waitingMember = new MemberProfile { UserId = waitingUser.Id, CenterId = center.Id,
        FullName = "Waiting Member", MemberCode = "NOTIFYWAITING", CreatedAt = DateTime.UtcNow };
    var notificationPackage = new MembershipPackage { CenterId = center.Id, Name = "Notification Package",
        Price = 100, DurationDays = 30, AllowedSports = 1, Status = "Active", CreatedAt = DateTime.UtcNow };
    db.MemberProfiles.Add(waitingMember); db.MembershipPackages.Add(notificationPackage); await db.SaveChangesAsync();
    var waitingToken = await Login(waitingUser);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    client.DefaultRequestHeaders.Add("Idempotency-Key", "notification-partial-checkout");
    var checkout = await Json(await client.PostAsJsonAsync("payments/counter-checkout", new {
        memberId = member.Id, packageId = notificationPackage.Id, paymentMethod = "CASH", amountReceived = 40
    }), HttpStatusCode.OK, "partial counter payment succeeds");
    Assert(!await (from link in db.UserNotifications join n in db.Notifications on link.NotificationId equals n.Id
        where link.UserId == memberUser.Id && n.NotificationType == "MembershipActivated" select link.Id).AnyAsync(),
        "partial payment does not falsely notify membership activation");
    var invoiceNumber = checkout.GetProperty("invoiceNumber").GetString();
    client.DefaultRequestHeaders.Remove("Idempotency-Key");
    client.DefaultRequestHeaders.Add("Idempotency-Key", "notification-complete-payment");
    for (var attempt = 0; attempt < 2; attempt++)
        await Json(await client.PostAsJsonAsync($"invoices/{invoiceNumber}/payments", new { amount = 60, paymentMethod = "CASH" }),
            HttpStatusCode.OK, "complete payment and retry succeed");
    client.DefaultRequestHeaders.Remove("Idempotency-Key");
    client.DefaultRequestHeaders.Add("Idempotency-Key", "notification-waiting-checkout");
    var waitingCheckout = new { memberId = waitingMember.Id, packageId = notificationPackage.Id, paymentMethod = "CASH", amountReceived = 100 };
    for (var attempt = 0; attempt < 2; attempt++)
        await Json(await client.PostAsJsonAsync("payments/counter-checkout", waitingCheckout), HttpStatusCode.OK, "full counter payment and retry succeed");
    client.DefaultRequestHeaders.Remove("Idempotency-Key");
    var notificationSession = MemberSession(memberToday.AddDays(2), 12);
    db.ClassSessions.Add(notificationSession); await db.SaveChangesAsync();
    // Capacity is scoped to this test and restored before testing class enrollment.
    await db.Classes.Where(c => c.Id == cls.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Capacity, 1));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
    var booked = await Json(await client.PostAsync($"class-sessions/{notificationSession.Id}/bookings", null), HttpStatusCode.Created, "booking creates member notification");
    await Json(await client.PostAsync($"class-sessions/{notificationSession.Id}/bookings", null), HttpStatusCode.Created, "booking retry succeeds");
    var bookedId = booked.GetProperty("booking").GetProperty("id").GetInt64();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", waitingToken);
    for (var attempt = 0; attempt < 2; attempt++)
        await Json(await client.PostAsync($"class-sessions/{notificationSession.Id}/bookings", null), HttpStatusCode.Accepted, "full session waitlist and retry succeed");
    await Json(await client.DeleteAsync($"session-bookings/{bookedId}"), HttpStatusCode.Forbidden, "foreign booking cancellation does not emit notifications");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
    await Json(await client.DeleteAsync($"session-bookings/{bookedId}"), HttpStatusCode.OK, "cancellation promotes waitlisted member");
    var bookingMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "member loads payment and booking notifications");
    foreach (var type in new[] { "MembershipActivated", "BookingConfirmed", "BookingCancelled" })
        Assert(bookingMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == type) == 1,
            $"{type} reaches the booking owner exactly once despite retries and denied requests");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", waitingToken);
    var waitingMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "waitlisted member loads notifications");
    foreach (var type in new[] { "MembershipActivated", "WaitlistJoined", "WaitlistPromoted" })
        Assert(waitingMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == type
            && !n.GetProperty("isRead").GetBoolean()) == 1, $"{type} reaches the waiting member exactly once and unread");
    Assert(!waitingMessages.EnumerateArray().Any(n => n.GetProperty("notificationType").GetString() == "BookingCancelled"),
        "waiting member never receives another member's cancellation");
    await db.Classes.Where(c => c.Id == cls.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Capacity, 10));
    var subscriptionId = await db.MemberSubscriptions.Where(s => s.MemberId == waitingMember.Id && s.Status == "Active").Select(s => s.Id).SingleAsync();
    var enrollment = await Json(await client.PostAsJsonAsync($"classes/{cls.Id}/enrollments", new { subscriptionId }), HttpStatusCode.Created, "class enrollment creates notification");
    await Json(await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"classes/{cls.Id}/enrollments/{enrollment.GetProperty("id").GetInt64()}") {
        Content = JsonContent.Create(new { reason = "Notification regression" }) }), HttpStatusCode.OK, "class enrollment cancellation creates notification");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    await Json(await client.PostAsJsonAsync($"centers/{center.Id}/support-requests", new {
        memberId = waitingMember.Id, subject = "", description = "Please help", priority = "Invalid"
    }), HttpStatusCode.BadRequest, "invalid support input returns validation error without notification");
    var support = await Json(await client.PostAsJsonAsync($"centers/{center.Id}/support-requests", new {
        memberId = waitingMember.Id, subject = "Notification Support", description = "Please help", priority = "Normal"
    }), HttpStatusCode.OK, "staff creates support request with member notification");
    var supportId = support.GetProperty("request").GetProperty("id").GetInt64();
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/support-requests/{supportId}", new { status = "Resolved", note = "Support reply delivered" }),
        HttpStatusCode.OK, "support response creates notification");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/support-requests/{supportId}", new { status = "Resolved" }),
        HttpStatusCode.OK, "unchanged support status does not create duplicate notification");
    await Json(await client.PatchAsJsonAsync($"centers/{center.Id}/support-requests/{supportId}", new { status = "Invalid" }),
        HttpStatusCode.BadRequest, "invalid support update returns validation error without notification");
    var supportPage = await Json(await client.GetAsync($"centers/{center.Id}/support-requests?query=Waiting&status=Resolved&pageSize=1"),
        HttpStatusCode.OK, "support request query filters and paginates in SQL");
    Assert(supportPage.GetProperty("totalCount").GetInt32() == 1 && supportPage.GetProperty("items")[0].GetProperty("id").GetInt64() == supportId,
        "support search returns the updated request by member name and status");
    await Json(await client.GetAsync($"centers/{otherCenter.Id}/support-requests/{supportId}"), HttpStatusCode.Forbidden,
        "support lookup remains restricted to the staff center");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", waitingToken);
    var finalMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "member reloads notifications from all new sources");
    Assert(finalMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == "SupportUpdated") == 2
        && finalMessages.EnumerateArray().Any(n => n.GetProperty("content").GetString()!.Contains("Support reply delivered")),
        "support creation and reply persist readable content without no-op duplicates");
    Assert(finalMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == "BookingConfirmed") == 1
        && finalMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == "BookingCancelled") == 1,
        "class enrollment notifications reach their owner");
    var supportNotificationId = finalMessages.EnumerateArray().First(n => n.GetProperty("notificationType").GetString() == "SupportUpdated").GetProperty("id").GetInt64();
    await Json(await client.PutAsync($"notifications/{supportNotificationId}/read", null), HttpStatusCode.NoContent, "new-source notification can be marked read");
    var reloadedMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "read notification reloads");
    Assert(reloadedMessages.EnumerateArray().Single(n => n.GetProperty("id").GetInt64() == supportNotificationId).GetProperty("isRead").GetBoolean(),
        "new-source read state persists");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
    var privateMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "unrelated member inbox remains private");
    Assert(privateMessages.GetArrayLength() == 0, "new notification sources never leak across recipients or centers");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coachToken);
    await Json(await client.PutAsJsonAsync("coaches/me/personal-training-plans", request with { memberId = member.Id, title = "Private notification plan" }),
        HttpStatusCode.OK, "personal plan update succeeds with multiple classmates");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", waitingToken);
    var classmateMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "classmate inbox loads after private plan update");
    Assert(!classmateMessages.EnumerateArray().Any(n => n.GetProperty("notificationType").GetString() == "TrainingPlanUpdated"),
        "personal training notification never reaches another member of the same class");
    var legacyPending = await Json(await client.PostAsJsonAsync("members/me/subscriptions", new { packageId = notificationPackage.Id }),
        HttpStatusCode.Created, "member requests another package for legacy cash API");
    var legacyInvoiceId = legacyPending.GetProperty("invoiceId").GetInt64();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
    for (var attempt = 0; attempt < 2; attempt++)
        await Json(await client.PostAsJsonAsync($"invoices/{legacyInvoiceId}/payments", new { amount = 100, idempotencyKey = "notification-legacy-cash-payment" }),
            HttpStatusCode.Created, "legacy cash payment and retry succeed");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", waitingToken);
    var legacyMessages = await Json(await client.GetAsync("notifications/me"), HttpStatusCode.OK, "legacy payment recipient loads notifications");
    Assert(legacyMessages.EnumerateArray().Count(n => n.GetProperty("notificationType").GetString() == "MembershipActivated") == 2,
        "legacy cash payment emits one activation notification and retry does not duplicate it");
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

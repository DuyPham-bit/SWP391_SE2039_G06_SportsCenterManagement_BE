using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

sealed class RecordingObjectStorage : AmazonS3Client
{
    public List<string> Uploaded { get; } = [];
    public List<string> Deleted { get; } = [];

    public RecordingObjectStorage() : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://127.0.0.1:1" }) { }

    public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
    {
        Uploaded.Add(request.Key);
        return Task.FromResult(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
    }

    public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
    {
        Deleted.Add(request.Key);
        return Task.FromResult(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });
    }
}

static class MediaSecurityChecks
{
    public static async Task Run(SportsCenterDbContext db, HttpClient http, JwtTokenIssuer issuer,
        RecordingObjectStorage objects, Func<string, Func<Task<bool>>, Task> check)
    {
        var a = new Center { Name = "Media center A", Address = "Audit", Status = "Active", CreatedAt = DateTime.UtcNow };
        var b = new Center { Name = "Media center B", Address = "Audit", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.AddRange(a, b); await db.SaveChangesAsync();
        async Task<User> Account(string name, string roleName, long? center) {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == roleName);
            if (role is null) { role = new Role { Name = roleName }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { Username = name, Email = name + "@example.test", RoleId = role.Id, Status = "Active",
                PasswordHash = "not-a-login-credential", CreatedAt = DateTime.UtcNow };
            db.Users.Add(user); await db.SaveChangesAsync();
            if (center.HasValue) db.StaffProfiles.Add(new StaffProfile { UserId = user.Id, CenterId = center.Value,
                StaffCode = name, FullName = name, Position = roleName, Status = "Active", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(); return user;
        }
        var alice = await Account("media-manager-a", "Manager", a.Id);
        var coworker = await Account("media-coworker-a", "Manager", a.Id);
        var bob = await Account("media-manager-b", "Manager", b.Id);
        var detached = await Account("media-no-center", "Manager", null);
        var admin = await Account("media-admin", "Admin", null);
        void Login(User user, string role = "Manager", long? claimedCenter = null) => http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", issuer.Issue(new(user.Id, user.Username, role, claimedCenter, user.Email, user.AuthVersion)).Token);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZV8AAAAASUVORK5CYII=");
        async Task<HttpResponseMessage> Upload(string route) {
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(png); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "audit.png"); return await http.PostAsync(route, form);
        }
        async Task<string> UploadKey(User user, string route, string role = "Manager") {
            Login(user, role); using var response = await Upload(route);
            if (response.StatusCode != HttpStatusCode.Created) throw new Exception($"Upload returned {(int)response.StatusCode}.");
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
        }
        async Task<long> UploadGallery(User user) {
            Login(user); using var response = await Upload("api/media/upload");
            if (!response.IsSuccessStatusCode) throw new Exception($"Media upload returned {(int)response.StatusCode}.");
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        }
        Task<HttpResponseMessage> Delete(string route, string key) => http.DeleteAsync(route + "?key=" + Uri.EscapeDataString(key));
        async Task<long[]> Gallery(string route = "api/media/files") {
            using var response = await http.GetAsync(route);
            if (!response.IsSuccessStatusCode) throw new Exception($"Gallery returned {(int)response.StatusCode}.");
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
        }

        var aliceBill = await UploadKey(alice, "api/refund-bills");
        var bobBill = await UploadKey(bob, "api/refund-bills");
        var aliceDashboard = await UploadKey(alice, "api/r2-images/dashboard");
        var bobDashboard = await UploadKey(bob, "api/r2-images/dashboard");
        await check("R2 refund deletion rejects another center before invoking object storage", async () => {
            Login(alice); var before = objects.Deleted.Count;
            return (await Delete("api/refund-bills", bobBill)).StatusCode == HttpStatusCode.NotFound && objects.Deleted.Count == before;
        });
        await check("R2 dashboard deletion rejects another center even with a forged center claim", async () => {
            Login(alice, claimedCenter: b.Id); var before = objects.Deleted.Count;
            return (await Delete("api/r2-images/dashboard", bobDashboard)).StatusCode == HttpStatusCode.NotFound && objects.Deleted.Count == before;
        });
        await check("R2 deletion permits a manager in the upload's center", async () => {
            Login(coworker); var before = objects.Deleted.Count;
            return (await Delete("api/refund-bills", aliceBill)).StatusCode == HttpStatusCode.NoContent
                && (await Delete("api/r2-images/dashboard", aliceDashboard)).StatusCode == HttpStatusCode.NoContent
                && objects.Deleted.Count == before + 2;
        });
        await check("R2 refuses unowned legacy keys and cross-purpose deletion by a manager", async () => {
            Login(alice); var before = objects.Deleted.Count;
            var unknown = await Delete("api/refund-bills", "refund-bills/2026/01/unknown.png");
            var wrongPurpose = await Delete("api/refund-bills", $"avatars/{alice.Id}/2026/01/unused.png");
            return unknown.StatusCode == HttpStatusCode.NotFound && wrongPurpose.StatusCode == HttpStatusCode.BadRequest && objects.Deleted.Count == before;
        });
        await check("avatar deletion remains restricted to the owner", async () => {
            var avatar = await UploadKey(alice, "api/r2-images/avatar");
            Login(bob); var before = objects.Deleted.Count;
            var foreign = await Delete("api/r2-images/avatar", avatar);
            if (foreign.IsSuccessStatusCode || objects.Deleted.Count != before) return false;
            Login(alice);
            return (await Delete("api/r2-images/avatar", avatar)).StatusCode == HttpStatusCode.NoContent && objects.Deleted.Count == before + 1;
        });
        await check("a manager without a center cannot upload business images", async () => {
            Login(detached); var before = objects.Uploaded.Count;
            return (await Upload("api/refund-bills")).StatusCode == HttpStatusCode.Forbidden && objects.Uploaded.Count == before;
        });
        await check("R2 metadata keeps its center when the uploader is transferred", async () => {
            var key = await UploadKey(alice, "api/refund-bills");
            var metadata = await db.UploadedMedias.AsNoTracking().SingleAsync(m => m.DriveFileId == "r2:" + key);
            if (metadata.UploadedByUserId != alice.Id || metadata.CenterId != a.Id) return false;
            await db.StaffProfiles.Where(p => p.UserId == alice.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.CenterId, b.Id));
            try {
                Login(alice); var before = objects.Deleted.Count;
                var denied = await Delete("api/refund-bills", key);
                if (denied.StatusCode != HttpStatusCode.NotFound || objects.Deleted.Count != before) return false;
                Login(coworker); return (await Delete("api/refund-bills", key)).StatusCode == HttpStatusCode.NoContent;
            } finally { await db.StaffProfiles.Where(p => p.UserId == alice.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.CenterId, a.Id)); }
        });
        await check("noncanonical R2 keys are rejected before object storage is invoked", async () => {
            Login(alice); var before = objects.Deleted.Count;
            foreach (var key in new[] { "refund-bills/../foreign.png", "refund-bills/x/./foreign.png", "refund-bills//foreign.png", "refund-bills/%2e%2e/foreign.png" })
                if ((await Delete("api/refund-bills", key)).StatusCode != HttpStatusCode.BadRequest) return false;
            return objects.Deleted.Count == before;
        });
        await check("R2 ownership lookup cannot authorize a different casing of an object key", async () => {
            var key = await UploadKey(alice, "api/r2-images/dashboard");
            var nameAt = key.LastIndexOf('/') + 1;
            var alias = key[..nameAt] + key[nameAt..].ToUpperInvariant();
            var before = objects.Deleted.Count;
            return (await Delete("api/r2-images/dashboard", alias)).StatusCode == HttpStatusCode.NotFound && objects.Deleted.Count == before;
        });
        // A pre-existing refund can prove the tenant of an old key that predates upload metadata.
        var memberId = await db.MemberProfiles.Select(p => p.Id).FirstAsync();
        var legacyInvoice = new Invoice { InvoiceNumber = "MEDIA-LEGACY", MemberId = memberId, CenterId = b.Id,
            Status = "Paid", IssuedAt = DateTime.UtcNow };
        db.Invoices.Add(legacyInvoice); await db.SaveChangesAsync();
        var legacyPayment = new Payment { InvoiceId = legacyInvoice.Id, MemberId = memberId, PaymentMethod = "CASH",
            Amount = 100, PaymentStatus = "Succeeded", CreatedAt = DateTime.UtcNow };
        db.Payments.Add(legacyPayment); await db.SaveChangesAsync();
        const string legacyProof = "refund-bills/2026/01/legacy-proof.png";
        db.PaymentRefunds.Add(new PaymentRefund { PaymentId = legacyPayment.Id, Amount = 100, Status = "Approved",
            IdempotencyKey = "media-legacy-proof", Reason = "Audit", RequestedBy = bob.Id, TransferProofUrl = legacyProof, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await check("legacy refund keys use their invoice center and remain deletable by that center only", async () => {
            Login(alice); var before = objects.Deleted.Count;
            var denied = await Delete("api/refund-bills", legacyProof);
            if (denied.StatusCode != HttpStatusCode.NotFound || objects.Deleted.Count != before) return false;
            Login(bob); return (await Delete("api/refund-bills", legacyProof)).StatusCode == HttpStatusCode.NoContent;
        });
        var conflictingProof = await UploadKey(alice, "api/refund-bills");
        db.PaymentRefunds.Add(new PaymentRefund { PaymentId = legacyPayment.Id, Amount = 1, Status = "Approved",
            IdempotencyKey = "media-conflicting-proof", Reason = "Audit", RequestedBy = bob.Id, TransferProofUrl = conflictingProof, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await check("refund deletion cannot damage another center's invoice even with conflicting upload metadata", async () => {
            Login(alice); var before = objects.Deleted.Count;
            var aResult = await Delete("api/refund-bills", conflictingProof);
            Login(bob); var bResult = await Delete("api/refund-bills", conflictingProof);
            return aResult.StatusCode == HttpStatusCode.NotFound && bResult.StatusCode == HttpStatusCode.NotFound && objects.Deleted.Count == before;
        });
        var reviewInvoice = new Invoice { InvoiceNumber = "MEDIA-REVIEW", MemberId = memberId, CenterId = a.Id,
            Status = "Paid", IssuedAt = DateTime.UtcNow };
        db.Invoices.Add(reviewInvoice); await db.SaveChangesAsync();
        var reviewPayment = new Payment { InvoiceId = reviewInvoice.Id, MemberId = memberId, PaymentMethod = "CASH",
            Amount = 100, PaymentStatus = "Succeeded", CreatedAt = DateTime.UtcNow };
        db.Payments.Add(reviewPayment); await db.SaveChangesAsync();
        async Task<PaymentRefund> PendingReview(string suffix) {
            var refund = new PaymentRefund { PaymentId = reviewPayment.Id, Amount = 1, Status = "Pending",
                IdempotencyKey = "media-review-" + suffix, Reason = "Audit", RequestedBy = alice.Id, CreatedAt = DateTime.UtcNow,
                BankName = "Audit bank", BankAccountNumber = "123456789", BankAccountName = "Audit account" };
            db.PaymentRefunds.Add(refund); await db.SaveChangesAsync(); return refund;
        }
        foreach (var (label, key) in new[] { ("foreign-center", bobBill), ("unknown", "refund-bills/2026/01/unbound.png"), ("legacy foreign-center", legacyProof) }) {
            await check($"refund review cannot bind a {label} key to gain media deletion rights", async () => {
                var refund = await PendingReview(label.Replace(' ', '-'));
                Login(alice);
                using var response = await http.PatchAsJsonAsync($"api/refund-requests/{refund.Id}", new { Status = "Approved", TransferProofUrl = key });
                return response.StatusCode == HttpStatusCode.NotFound && await db.PaymentRefunds.AsNoTracking()
                    .AnyAsync(r => r.Id == refund.Id && r.Status == "Pending" && r.TransferProofUrl == null);
            });
        }
        await check("refund review still approves a newly uploaded proof from the same center", async () => {
            var key = await UploadKey(alice, "api/refund-bills");
            var refund = await PendingReview("allowed");
            using var response = await http.PatchAsJsonAsync($"api/refund-requests/{refund.Id}", new { Status = "Approved", TransferProofUrl = key });
            return response.IsSuccessStatusCode && await db.PaymentRefunds.AsNoTracking()
                .AnyAsync(r => r.Id == refund.Id && r.Status == "Approved" && r.TransferProofUrl == key);
        });
        await check("an Admin's unassigned R2 upload cannot be claimed through a manager's invoice", async () => {
            var key = await UploadKey(admin, "api/refund-bills", "Admin");
            var refund = await PendingReview("admin-upload");
            refund.Status = "Approved"; refund.TransferProofUrl = key; await db.SaveChangesAsync();
            Login(alice); var before = objects.Deleted.Count;
            return (await Delete("api/refund-bills", key)).StatusCode == HttpStatusCode.NotFound && objects.Deleted.Count == before;
        });
        var aliceImage = await UploadGallery(alice);
        var coworkerImage = await UploadGallery(coworker);
        var bobImage = await UploadGallery(bob);
        db.UploadedMedias.Add(new UploadedMedia { DriveFileId = "legacy-unowned", FileName = "legacy.png", MimeType = "image/png",
            FileUrl = "https://media.example.test/legacy.png", CreatedAt = DateTime.UtcNow.AddHours(1) });
        await db.SaveChangesAsync();
        await check("the media gallery shares only the caller's center and excludes unowned and R2 invoice files", async () => {
            Login(alice); var ids = await Gallery();
            return ids.ToHashSet().SetEquals(new[] { aliceImage, coworkerImage });
        });
        await check("recent media applies the center filter before its limit", async () => {
            Login(alice); var ids = await Gallery("api/media/recent?limit=1");
            return ids.Length == 1 && ids[0] == coworkerImage;
        });
        await check("media status does not expose another center's image count", async () => {
            Login(alice); using var response = await http.GetAsync("api/media/status");
            return response.IsSuccessStatusCode && (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalImages").GetInt32() == 2;
        });
        await check("Admin retains a global media view and R2 management", async () => {
            Login(admin, "Admin"); var ids = await Gallery();
            return ids.Contains(aliceImage) && ids.Contains(coworkerImage) && ids.Contains(bobImage)
                && (await Delete("api/refund-bills", bobBill)).StatusCode == HttpStatusCode.NoContent;
        });
        http.DefaultRequestHeaders.Authorization = null;
    }
}

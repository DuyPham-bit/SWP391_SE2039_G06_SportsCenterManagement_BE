using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Controllers;

[Authorize(Roles = "MANAGER,ADMIN")]
public sealed class ClassResourcesController(SportsCenterDbContext db, IWebHostEnvironment environment) : FlowControllerBase
{
    private const long MaxResourceImageSize = 5 * 1024 * 1024;

    [AllowAnonymous]
    [HttpGet("public/centers/{centerId:long}/resources")]
    public async Task<IActionResult> PublicResources(long centerId, CancellationToken ct)
    {
        if (!await db.Centers.AnyAsync(center => center.Id == centerId, ct)) return NotFound();

        Response.Headers.CacheControl = "no-store";
        var sports = await db.Sports.Where(s => (s.CenterId == centerId
                || (s.CenterId == null && !db.Sports.Any(local => local.CenterId == centerId && local.Name == s.Name)))
                && s.Status == "Active")
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Description, s.ImageUrl })
            .ToListAsync(ct);
        var rooms = await db.Rooms.Where(room => room.CenterId == centerId && room.Status == "Active")
            .OrderBy(room => room.Name)
            .Select(room => new { room.Id, room.Name, Type = room.RoomType, room.Capacity,
                Location = room.LocationDescription, room.ImageUrl })
            .ToListAsync(ct);

        var classes = await (from cls in db.Classes
            join sport in db.Sports on cls.SportId equals sport.Id
            where cls.CenterId == centerId && cls.Status == "Published" && sport.Status == "Active"
            orderby cls.Name
            select new { cls.Id, cls.Name, cls.Description, cls.Level, cls.DurationMinutes,
                SportName = sport.Name, cls.ImageUrl }).ToListAsync(ct);
        var coaches = await (from coach in db.CoachProfiles
            join user in db.Users on coach.UserId equals user.Id
            where coach.CenterId == centerId && coach.Status == "Active" && user.Status == "Active"
            orderby coach.FullName
            select new { coach.Id, coach.FullName, coach.Specialization, coach.Certification, coach.Bio, coach.ImageUrl })
            .ToListAsync(ct);
        var packages = await db.MembershipPackages.Where(package => package.CenterId == centerId && package.Status == "Active")
            .OrderBy(package => package.Price)
            .Select(package => new { package.Id, package.Name, package.Description, package.DurationDays,
                package.Price, package.MaxClasses, package.AccessType, package.AllowedSports, package.Badge })
            .ToListAsync(ct);

        return Ok(new { sports, rooms, classes, coaches, packages });
    }

    [HttpPost("centers/{centerId:long}/resources/images")]
    [RequestSizeLimit(MaxResourceImageSize + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxResourceImageSize + 64 * 1024)]
    public async Task<IActionResult> UploadResourceImage(
        long centerId,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        if (file is null || file.Length == 0 || file.Length > MaxResourceImageSize)
            throw FlowException.BadRequest("Chá»n áº£nh cÃ³ dung lÆ°á»£ng tá»« 1 byte Ä‘áº¿n 5 MB.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await input.ReadAsync(header.AsMemory(0, header.Length), ct);
        var extension = GetImageExtension(file.ContentType, header, bytesRead);
        if (extension is null)
            throw FlowException.BadRequest("áº¢nh pháº£i lÃ  PNG, JPEG, GIF hoáº·c WebP há»£p lá»‡.");

        var fileName = $"c{centerId}_{Guid.NewGuid():N}{extension}";
        var directory = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "resources");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, fileName);
        input.Position = 0;
        try
        {
            await using var output = System.IO.File.Create(destination);
            await input.CopyToAsync(output, ct);
        }
        catch
        {
            if (System.IO.File.Exists(destination)) System.IO.File.Delete(destination);
            throw;
        }

        var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/resources/{fileName}";
        return Ok(new { imageUrl });
    }

    [HttpGet("/uploads/resources/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetResourceImage(string fileName)
    {
        if (Path.GetFileName(fileName) != fileName
            || !System.Text.RegularExpressions.Regex.IsMatch(fileName,
                @"^c[1-9][0-9]*_[a-f0-9]{32}\.(png|jpg|gif|webp)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return NotFound();

        var root = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var target = Path.GetFullPath(Path.Combine(root, "uploads", "resources", fileName));
        var directory = Path.GetFullPath(Path.Combine(root, "uploads", "resources")) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(directory, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(target))
            return NotFound();
        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
        return PhysicalFile(target, contentType);
    }

    [HttpDelete("centers/{centerId:long}/resources/images")]
    public async Task<IActionResult> DeleteUnusedResourceImage(
        long centerId,
        [FromQuery] string? imageUrl,
        CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        await DeleteUploadedImageIfUnusedAsync(centerId, imageUrl, ct);
        return NoContent();
    }

    [HttpGet("centers/{centerId:long}/classes/all")]
    public async Task<IActionResult> Classes(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await (from cls in db.Classes
            join sport in db.Sports on cls.SportId equals sport.Id
            where cls.CenterId == centerId
            orderby cls.Name
            select new { cls.Id, cls.SportId, SportName = sport.Name, cls.RoomId, cls.Name,
                cls.Description, cls.Level, cls.Capacity, cls.DurationMinutes, cls.Status, cls.AllowWaitlist, cls.ImageUrl,
                EnrolledCount = db.ClassEnrollments.Count(e => e.ClassId == cls.Id && e.Status == "Confirmed"),
                RoomName = db.Rooms.Where(r => r.Id == cls.RoomId).Select(r => r.Name).FirstOrDefault(),
                CoachId = db.ClassCoaches.Where(c => c.ClassId == cls.Id && c.IsPrimary).Select(c => (long?)c.CoachId).FirstOrDefault(),
                CoachName = (from a in db.ClassCoaches join c in db.CoachProfiles on a.CoachId equals c.Id
                    where a.ClassId == cls.Id && a.IsPrimary select c.FullName).FirstOrDefault()
            }).ToListAsync(ct));
    }

    [HttpGet("centers/{centerId:long}/sports")]
    public async Task<IActionResult> Sports(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await db.Sports.Where(s => (s.CenterId == centerId
                || (s.CenterId == null && !db.Sports.Any(local => local.CenterId == centerId && local.Name == s.Name)))
                && s.Status == "Active")
            .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Description, s.ImageUrl }).ToListAsync(ct));
    }

    [HttpGet("centers/{centerId:long}/coaches")]
    public async Task<IActionResult> Coaches(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await (from coach in db.CoachProfiles join user in db.Users on coach.UserId equals user.Id
            where coach.CenterId == centerId && coach.Status == "Active" && user.Status == "Active"
            orderby coach.FullName
            select new { coach.Id, coach.UserId, coach.FullName, coach.Specialization, coach.Certification, coach.ImageUrl }).ToListAsync(ct));
    }

    [HttpGet("centers/{centerId:long}/sports/all")]
    public async Task<IActionResult> AllSports(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await db.Sports.Where(s => s.CenterId == centerId
                || (s.CenterId == null && !db.Sports.Any(local => local.CenterId == centerId && local.Name == s.Name)))
            .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Description, s.ImageUrl, s.Status,
                IsShared = s.CenterId == null }).ToListAsync(ct));
    }

    public sealed record SportRequest(string Name, string? Description, string Status = "Active", string? ImageUrl = null);

    [HttpPost("centers/{centerId:long}/sports")]
    public async Task<IActionResult> CreateSport(long centerId, SportRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        ValidateSport(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await EnsureSportNameAvailable(centerId, null, request.Name.Trim(), ct);
        var sport = new Sport { CenterId = centerId, Name = request.Name.Trim(),
            Description = request.Description?.Trim(), ImageUrl = NormalizeImageUrl(request.ImageUrl), Status = request.Status };
        db.Sports.Add(sport);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "SPORT.CREATED",
            EntityType = "Sport", EntityId = sport.Id, NewValues = System.Text.Json.JsonSerializer.Serialize(request), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new { sport.Id, sport.Name, sport.Description, sport.ImageUrl, sport.Status, IsShared = false });
    }

    [HttpPut("centers/{centerId:long}/sports/{sportId:long}")]
    public async Task<IActionResult> UpdateSport(long centerId, long sportId, SportRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        ValidateSport(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var sport = await db.Sports.SingleOrDefaultAsync(s => s.Id == sportId
                && (s.CenterId == centerId || s.CenterId == null), ct)
            ?? throw FlowException.NotFound("Chỉ được sửa bộ môn riêng của trung tâm này.");
        var isShared = sport.CenterId is null;
        await EnsureSportNameAvailable(centerId, isShared ? null : sportId, request.Name.Trim(), ct);
        if (request.Status != "Active" && await db.Classes.AnyAsync(c => c.CenterId == centerId && c.SportId == sportId
            && c.Status != "Cancelled" && c.Status != "Completed", ct))
            throw FlowException.Conflict("Bộ môn đang có lớp hoạt động; cần đóng các lớp trước khi ngừng sử dụng.");
        var before = new { sport.Name, sport.Description, sport.ImageUrl, sport.Status };
        if (isShared)
        {
            var localSport = new Sport
            {
                CenterId = centerId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                ImageUrl = NormalizeImageUrl(request.ImageUrl),
                Status = request.Status
            };
            db.Sports.Add(localSport);
            await db.SaveChangesAsync(ct);
            var localClasses = await db.Classes.Where(item => item.CenterId == centerId && item.SportId == sportId)
                .ToListAsync(ct);
            foreach (var localClass in localClasses) localClass.SportId = localSport.Id;
            db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "SPORT.CUSTOMIZED",
                EntityType = "Sport", EntityId = localSport.Id, OldValues = System.Text.Json.JsonSerializer.Serialize(before),
                NewValues = System.Text.Json.JsonSerializer.Serialize(request), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Ok(new { localSport.Id, localSport.Name, localSport.Description, localSport.ImageUrl,
                localSport.Status, IsShared = false });
        }
        var oldImageUrl = sport.ImageUrl;
        sport.Name = request.Name.Trim(); sport.Description = request.Description?.Trim();
        sport.ImageUrl = NormalizeImageUrl(request.ImageUrl); sport.Status = request.Status;
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "SPORT.UPDATED",
            EntityType = "Sport", EntityId = sport.Id, OldValues = System.Text.Json.JsonSerializer.Serialize(before),
            NewValues = System.Text.Json.JsonSerializer.Serialize(request), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await DeleteUploadedImageIfUnusedAsync(centerId, oldImageUrl, ct);
        return Ok(new { sport.Id, sport.Name, sport.Description, sport.ImageUrl, sport.Status, IsShared = false });
    }

    [HttpDelete("centers/{centerId:long}/sports/{sportId:long}")]
    public async Task<IActionResult> DeleteSport(long centerId, long sportId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var sport = await db.Sports.SingleOrDefaultAsync(s => s.Id == sportId
                && (s.CenterId == centerId || s.CenterId == null), ct)
            ?? throw FlowException.NotFound("Chỉ được xóa bộ môn riêng của trung tâm này.");
        if (sport.Status == "Inactive")
            throw FlowException.Conflict("Bộ môn đã được ngừng hiển thị.");
        if (await db.Classes.AnyAsync(c => c.CenterId == centerId && c.SportId == sportId
            && c.Status != "Cancelled" && c.Status != "Completed", ct))
            throw FlowException.Conflict("Bộ môn đang có lớp hoạt động; hãy đóng các lớp trước khi xóa.");

        var before = new { sport.Name, sport.Description, sport.ImageUrl, sport.Status };
        if (sport.CenterId is null)
        {
            if (await db.Sports.AnyAsync(item => item.CenterId == centerId && item.Name == sport.Name, ct))
                throw FlowException.Conflict("Bá»™ mÃ´n Ä‘Ã£ cÃ³ tÃ¹y chá»‰nh riÃªng cho trung tÃ¢m.");
            var localSport = new Sport { CenterId = centerId, Name = sport.Name, Description = sport.Description,
                ImageUrl = sport.ImageUrl, Status = "Inactive" };
            db.Sports.Add(localSport);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "SPORT.HIDDEN",
                EntityType = "Sport", EntityId = localSport.Id, OldValues = System.Text.Json.JsonSerializer.Serialize(before),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { localSport.Status }), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return NoContent();
        }
        sport.Status = "Inactive";
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "SPORT.DELETED",
            EntityType = "Sport", EntityId = sport.Id, OldValues = System.Text.Json.JsonSerializer.Serialize(before),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { sport.Status }), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    private async Task EnsureSportNameAvailable(long centerId, long? sportId, string name, CancellationToken ct)
    {
        if (await db.Sports.AnyAsync(s => s.Id != sportId && s.CenterId == centerId
            && s.Name == name, ct)) throw FlowException.Conflict("Tên bộ môn đã tồn tại.");
    }

    private static void ValidateSport(SportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100
            || request.Description?.Length > 500 || request.Status is not ("Active" or "Inactive")
            || !IsValidImageUrl(request.ImageUrl))
            throw FlowException.BadRequest("Tên bộ môn bắt buộc, tối đa 100 ký tự; mô tả tối đa 500; ảnh phải là URL HTTP(S) hợp lệ tối đa 2048 ký tự; trạng thái Active hoặc Inactive.");
    }

    [HttpGet("centers/{centerId:long}/rooms")]
    public async Task<IActionResult> Rooms(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await db.Rooms.Where(r => r.CenterId == centerId).OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, Type = r.RoomType, r.Capacity, Location = r.LocationDescription, r.ImageUrl, r.Status }).ToListAsync(ct));
    }

    public sealed record RoomRequest(string Name, string? Type, int Capacity, string? Location,
        string? ImageUrl = null, string Status = "Active");

    [HttpPost("centers/{centerId:long}/rooms")]
    public async Task<IActionResult> CreateRoom(long centerId, RoomRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        Validate(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (await db.Rooms.AnyAsync(r => r.CenterId == centerId && r.Name == request.Name.Trim(), ct))
            throw FlowException.Conflict("Tên phòng đã tồn tại trong trung tâm.");
        var room = new Room { CenterId = centerId, Name = request.Name.Trim(), RoomType = request.Type?.Trim(),
            Capacity = request.Capacity, LocationDescription = request.Location?.Trim(),
            ImageUrl = NormalizeImageUrl(request.ImageUrl), Status = request.Status, CreatedAt = DateTime.UtcNow };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "CREATE_ROOM",
            EntityType = "Room", EntityId = room.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new { room.Id, room.Name, Type = room.RoomType, room.Capacity, Location = room.LocationDescription, room.ImageUrl, room.Status });
    }

    [HttpPut("centers/{centerId:long}/rooms/{roomId:long}")]
    public async Task<IActionResult> UpdateRoom(long centerId, long roomId, RoomRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        Validate(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId && r.CenterId == centerId, ct)
            ?? throw FlowException.NotFound("Không tìm thấy phòng.");
        if (await db.Rooms.AnyAsync(r => r.CenterId == centerId && r.Id != roomId && r.Name == request.Name.Trim(), ct))
            throw FlowException.Conflict("Tên phòng đã tồn tại trong trung tâm.");
        if (request.Status != "Active") await EnsureRoomCanBeHiddenAsync(centerId, roomId, ct);
        // Capacity changes must preserve the seats already promised in future sessions.
        if (await (from s in db.ClassSessions join c in db.Classes on s.ClassId equals c.Id
            where (s.RoomId ?? c.RoomId) == roomId && s.SessionDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
                && s.SessionStatus == "Scheduled"
                && db.SessionBookings.Count(b => b.SessionId == s.Id && b.Status == "Booked") > request.Capacity
            select s.Id).AnyAsync(ct))
            throw FlowException.Conflict("Sức chứa mới nhỏ hơn số chỗ đã đặt trong một buổi sắp tới.");
        var oldImageUrl = room.ImageUrl;
        var before = new { room.Name, room.RoomType, room.Capacity, room.LocationDescription, room.ImageUrl, room.Status };
        room.Name = request.Name.Trim(); room.RoomType = request.Type?.Trim();
        room.Capacity = request.Capacity; room.LocationDescription = request.Location?.Trim();
        room.ImageUrl = NormalizeImageUrl(request.ImageUrl); room.Status = request.Status;
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "UPDATE_ROOM",
            EntityType = "Room", EntityId = room.Id, OldValues = System.Text.Json.JsonSerializer.Serialize(before),
            NewValues = System.Text.Json.JsonSerializer.Serialize(request), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await DeleteUploadedImageIfUnusedAsync(centerId, oldImageUrl, ct);
        return Ok(new { room.Id, room.Name, Type = room.RoomType, room.Capacity, Location = room.LocationDescription, room.ImageUrl, room.Status });
    }

    [HttpDelete("centers/{centerId:long}/rooms/{roomId:long}")]
    public async Task<IActionResult> DeleteRoom(long centerId, long roomId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId && r.CenterId == centerId, ct)
            ?? throw FlowException.NotFound("Không tìm thấy sân/phòng.");
        if (room.Status == "Inactive")
            throw FlowException.Conflict("Sân/phòng đã được ngừng hiển thị.");
        await EnsureRoomCanBeHiddenAsync(centerId, roomId, ct);

        var before = new { room.Name, room.RoomType, room.Capacity, room.LocationDescription, room.ImageUrl, room.Status };
        room.Status = "Inactive";
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "ROOM.DELETED",
            EntityType = "Room", EntityId = room.Id, OldValues = System.Text.Json.JsonSerializer.Serialize(before),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { room.Status }), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    private async Task EnsureRoomCanBeHiddenAsync(long centerId, long roomId, CancellationToken ct)
    {
        if (await db.Classes.AnyAsync(c => c.CenterId == centerId && c.RoomId == roomId
            && c.Status != "Cancelled" && c.Status != "Completed", ct))
            throw FlowException.Conflict("Sân/phòng đang được lớp hoạt động sử dụng; hãy chuyển phòng hoặc đóng lớp trước.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (await (from session in db.ClassSessions
            join cls in db.Classes on session.ClassId equals cls.Id
            where cls.CenterId == centerId && (session.RoomId ?? cls.RoomId) == roomId
                && session.SessionDate >= today && session.SessionStatus == "Scheduled"
            select session.Id).AnyAsync(ct))
            throw FlowException.Conflict("Sân/phòng có buổi tập sắp diễn ra; hãy chuyển lịch trước khi ẩn.");
    }

    private static void Validate(RoomRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100 || r.Capacity < 1
            || r.Type?.Length > 100 || r.Location?.Length > 255
            || r.Status is not ("Active" or "Inactive") || !IsValidImageUrl(r.ImageUrl))
            throw FlowException.BadRequest("Tên sàn/phòng bắt buộc (tối đa 100 ký tự), sức chứa phải dương, loại tối đa 100, vị trí tối đa 255, ảnh phải là URL HTTP(S) hợp lệ tối đa 2048 ký tự và trạng thái Active hoặc Inactive.");
    }

    private static bool IsValidImageUrl(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Trim().Length <= 2048
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? NormalizeImageUrl(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetImageExtension(string? contentType, byte[] header, int length)
    {
        contentType = contentType?.Trim().ToLowerInvariant();
        if (contentType == "image/png" && length >= 8
            && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (contentType == "image/jpeg" && length >= 3
            && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff) return ".jpg";
        if (contentType == "image/gif" && length >= 6
            && header.AsSpan(0, 6).SequenceEqual("GIF87a"u8)) return ".gif";
        if (contentType == "image/webp" && length >= 12
            && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }

    private async Task DeleteUploadedImageIfUnusedAsync(long centerId, string? imageUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)) return;
        const string prefix = "/uploads/resources/";
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;
        if (await db.Sports.AnyAsync(item => item.ImageUrl == imageUrl, ct)
            || await db.Rooms.AnyAsync(item => item.ImageUrl == imageUrl, ct)
            || await db.Classes.AnyAsync(item => item.ImageUrl == imageUrl, ct)
            || await db.CoachProfiles.AnyAsync(item => item.ImageUrl == imageUrl, ct)) return;

        var fileName = Path.GetFileName(path[prefix.Length..]);
        if (string.IsNullOrWhiteSpace(fileName)
            || !fileName.StartsWith($"c{centerId}_", StringComparison.OrdinalIgnoreCase)) return;
        var root = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var target = Path.GetFullPath(Path.Combine(root, "uploads", "resources", fileName));
        var resourceDirectory = Path.GetFullPath(Path.Combine(root, "uploads", "resources")) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(resourceDirectory, StringComparison.OrdinalIgnoreCase)) return;
        if (System.IO.File.Exists(target)) System.IO.File.Delete(target);
    }
}

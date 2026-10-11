using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Controllers;

[Authorize(Roles = "MANAGER,ADMIN")]
public sealed class ClassResourcesController(SportsCenterDbContext db) : FlowControllerBase
{
    [HttpGet("centers/{centerId:long}/classes/all")]
    public async Task<IActionResult> Classes(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await (from cls in db.Classes
            join sport in db.Sports on cls.SportId equals sport.Id
            where cls.CenterId == centerId
            orderby cls.Name
            select new { cls.Id, cls.SportId, SportName = sport.Name, cls.RoomId, cls.Name,
                cls.Description, cls.Level, cls.Capacity, cls.DurationMinutes, cls.Status, cls.AllowWaitlist,
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
        return Ok(await db.Sports.Where(s => (s.CenterId == null || s.CenterId == centerId) && s.Status == "Active")
            .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Description }).ToListAsync(ct));
    }

    [HttpGet("centers/{centerId:long}/coaches")]
    public async Task<IActionResult> Coaches(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await (from coach in db.CoachProfiles join user in db.Users on coach.UserId equals user.Id
            where coach.CenterId == centerId && coach.Status == "Active" && user.Status == "Active"
            orderby coach.FullName
            select new { coach.Id, coach.UserId, coach.FullName, coach.Specialization, coach.Certification }).ToListAsync(ct));
    }

    [HttpGet("centers/{centerId:long}/sports/all")]
    public async Task<IActionResult> AllSports(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await db.Sports.Where(s => s.CenterId == null || s.CenterId == centerId)
            .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Description, s.Status,
                IsShared = s.CenterId == null }).ToListAsync(ct));
    }

    public sealed record SportRequest(string Name, string? Description, string Status = "Active");

    [HttpPost("centers/{centerId:long}/sports")]
    public async Task<IActionResult> CreateSport(long centerId, SportRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        ValidateSport(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await EnsureSportNameAvailable(centerId, null, request.Name.Trim(), ct);
        var sport = new Sport { CenterId = centerId, Name = request.Name.Trim(),
            Description = request.Description?.Trim(), Status = request.Status };
        db.Sports.Add(sport);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "SPORT.CREATED",
            EntityType = "Sport", EntityId = sport.Id, NewValues = System.Text.Json.JsonSerializer.Serialize(request), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new { sport.Id, sport.Name, sport.Description, sport.Status, IsShared = false });
    }

    [HttpPut("centers/{centerId:long}/sports/{sportId:long}")]
    public async Task<IActionResult> UpdateSport(long centerId, long sportId, SportRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        ValidateSport(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var sport = await db.Sports.SingleOrDefaultAsync(s => s.Id == sportId && s.CenterId == centerId, ct)
            ?? throw FlowException.NotFound("Chỉ được sửa bộ môn riêng của trung tâm này.");
        await EnsureSportNameAvailable(centerId, sportId, request.Name.Trim(), ct);
        if (request.Status != "Active" && await db.Classes.AnyAsync(c => c.SportId == sportId
            && c.Status != "Cancelled" && c.Status != "Completed", ct))
            throw FlowException.Conflict("Bộ môn đang có lớp hoạt động; cần đóng các lớp trước khi ngừng sử dụng.");
        var before = new { sport.Name, sport.Description, sport.Status };
        sport.Name = request.Name.Trim(); sport.Description = request.Description?.Trim(); sport.Status = request.Status;
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "SPORT.UPDATED",
            EntityType = "Sport", EntityId = sport.Id, OldValues = System.Text.Json.JsonSerializer.Serialize(before),
            NewValues = System.Text.Json.JsonSerializer.Serialize(request), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { sport.Id, sport.Name, sport.Description, sport.Status, IsShared = false });
    }

    private async Task EnsureSportNameAvailable(long centerId, long? sportId, string name, CancellationToken ct)
    {
        if (await db.Sports.AnyAsync(s => s.Id != sportId && (s.CenterId == centerId || s.CenterId == null)
            && s.Name == name, ct)) throw FlowException.Conflict("Tên bộ môn đã tồn tại.");
    }

    private static void ValidateSport(SportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100
            || request.Description?.Length > 500 || request.Status is not ("Active" or "Inactive"))
            throw FlowException.BadRequest("Tên bộ môn bắt buộc, tối đa 100 ký tự; mô tả tối đa 500; trạng thái Active hoặc Inactive.");
    }

    [HttpGet("centers/{centerId:long}/rooms")]
    public async Task<IActionResult> Rooms(long centerId, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        return Ok(await db.Rooms.Where(r => r.CenterId == centerId).OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, Type = r.RoomType, r.Capacity, Location = r.LocationDescription, r.Status }).ToListAsync(ct));
    }

    public sealed record RoomRequest(string Name, string? Type, int Capacity, string? Location);

    [HttpPost("centers/{centerId:long}/rooms")]
    public async Task<IActionResult> CreateRoom(long centerId, RoomRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, ct);
        Validate(request);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (await db.Rooms.AnyAsync(r => r.CenterId == centerId && r.Name == request.Name.Trim(), ct))
            throw FlowException.Conflict("Tên phòng đã tồn tại trong trung tâm.");
        var room = new Room { CenterId = centerId, Name = request.Name.Trim(), RoomType = request.Type?.Trim(),
            Capacity = request.Capacity, LocationDescription = request.Location?.Trim(), Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "CREATE_ROOM",
            EntityType = "Room", EntityId = room.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new { room.Id, room.Name, Type = room.RoomType, room.Capacity, Location = room.LocationDescription, room.Status });
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
        // Capacity changes must preserve the seats already promised in future sessions.
        if (await (from s in db.ClassSessions join c in db.Classes on s.ClassId equals c.Id
            where (s.RoomId ?? c.RoomId) == roomId && s.SessionDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
                && s.SessionStatus == "Scheduled"
                && db.SessionBookings.Count(b => b.SessionId == s.Id && b.Status == "Booked") > request.Capacity
            select s.Id).AnyAsync(ct))
            throw FlowException.Conflict("Sức chứa mới nhỏ hơn số chỗ đã đặt trong một buổi sắp tới.");
        room.Name = request.Name.Trim(); room.RoomType = request.Type?.Trim();
        room.Capacity = request.Capacity; room.LocationDescription = request.Location?.Trim();
        db.AuditLogs.Add(new AuditLog { UserId = CurrentUserId, CenterId = centerId, Action = "UPDATE_ROOM",
            EntityType = "Room", EntityId = room.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { room.Id, room.Name, Type = room.RoomType, room.Capacity, Location = room.LocationDescription, room.Status });
    }

    private static void Validate(RoomRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100 || r.Capacity < 1
            || r.Type?.Length > 100 || r.Location?.Length > 255)
            throw FlowException.BadRequest("Tên phòng bắt buộc (tối đa 100 ký tự), sức chứa phải dương, loại tối đa 100 và vị trí tối đa 255 ký tự.");
    }
}

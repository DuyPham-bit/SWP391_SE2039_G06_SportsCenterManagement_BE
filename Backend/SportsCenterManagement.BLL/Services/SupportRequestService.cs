using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Support;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class SupportRequestService(IUnitOfWork unitOfWork)
{
    private async Task EnsureStaff(long actorId, long centerId, CancellationToken ct)
    {
        var allowed = await (from staff in unitOfWork.Context.StaffProfiles
            join user in unitOfWork.Context.Users on staff.UserId equals user.Id
            join role in unitOfWork.Context.Roles on user.RoleId equals role.Id
            join center in unitOfWork.Context.Centers on staff.CenterId equals center.Id
            where user.Id == actorId && staff.CenterId == centerId && staff.Status == "Active"
                && user.Status == "Active" && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow)
                && center.Status == "Active" && (role.Name == "Receptionist" || role.Name == "Manager")
            select staff.Id).AnyAsync(ct);
        if (!allowed) throw FlowException.Forbidden("Bạn không có quyền tiếp nhận yêu cầu tại trung tâm này.");
    }

    private IQueryable<SupportSummary> Summaries(long centerId) =>
        from request in unitOfWork.Context.SupportRequests.AsNoTracking()
        join member in unitOfWork.Context.MemberProfiles.AsNoTracking() on request.MemberId equals member.Id
        where member.CenterId == centerId
        select new SupportSummary(request.Id, member.Id, member.FullName, member.MemberCode,
            request.Subject, request.Priority, request.Status, request.CreatedAt, request.ResolvedAt);

    public async Task<SupportPage> List(long actorId, long centerId, string? query, string? status, int page, int pageSize, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100 || query?.Length > 200)
            throw FlowException.BadRequest("Bộ lọc hoặc phân trang không hợp lệ.");
        if (!string.IsNullOrEmpty(status) && status is not ("Open" or "InProgress" or "Resolved"))
            throw FlowException.BadRequest("Trạng thái yêu cầu không hợp lệ.");
        var rows = Summaries(centerId);
        var term = query?.Trim();
        if (!string.IsNullOrEmpty(term)) rows = rows.Where(r => r.Subject.Contains(term) || r.MemberName.Contains(term) || r.MemberCode.Contains(term));
        if (!string.IsNullOrEmpty(status)) rows = rows.Where(r => r.Status == status);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new SupportPage(items, count, page, pageSize);
    }

    public async Task<SupportDetails> Get(long actorId, long centerId, long id, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        var summary = await Summaries(centerId).SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw FlowException.NotFound("Không tìm thấy yêu cầu hỗ trợ tại trung tâm này.");
        var description = await unitOfWork.Context.SupportRequests.Where(r => r.Id == id).Select(r => r.Description).SingleAsync(ct);
        var messages = await (from message in unitOfWork.Context.SupportRequestMessages.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on message.SenderId equals user.Id
            where message.RequestId == id orderby message.CreatedAt, message.Id
            select new SupportMessage(message.Id, user.Username, message.Message, message.CreatedAt)).ToListAsync(ct);
        return new SupportDetails(summary, description, messages);
    }

    public async Task<SupportDetails> Create(long actorId, long centerId, CreateSupportRequest body, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        if (string.IsNullOrWhiteSpace(body.Subject) || string.IsNullOrWhiteSpace(body.Description)
            || body.Subject.Length > 200 || body.Description.Length > 5000 || body.Priority is not ("Low" or "Normal" or "High"))
            throw FlowException.BadRequest("Nhập tiêu đề, nội dung và mức ưu tiên hợp lệ.");
        if (!await unitOfWork.Context.MemberProfiles.AnyAsync(m => m.Id == body.MemberId && m.CenterId == centerId, ct))
            throw FlowException.NotFound("Không tìm thấy thành viên tại trung tâm này.");
        await using var tx = await unitOfWork.Context.Database.BeginTransactionAsync(ct);
        var request = new SupportRequest { MemberId = body.MemberId, HandledBy = actorId, Subject = body.Subject.Trim(),
            Description = body.Description.Trim(), Priority = body.Priority, Status = "Open", CreatedAt = DateTime.UtcNow };
        unitOfWork.Context.SupportRequests.Add(request);
        await unitOfWork.SaveChangesAsync(ct);
        Audit(actorId, centerId, request, "Support.Created");
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await Get(actorId, centerId, request.Id, ct);
    }

    public async Task<SupportDetails> Update(long actorId, long centerId, long id, UpdateSupportRequest body, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        if (body.Status is not ("Open" or "InProgress" or "Resolved") || body.Note?.Length > 5000)
            throw FlowException.BadRequest("Trạng thái hoặc ghi chú không hợp lệ.");
        await using var tx = await unitOfWork.Context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var request = await (from r in unitOfWork.Context.SupportRequests
            join m in unitOfWork.Context.MemberProfiles on r.MemberId equals m.Id
            where r.Id == id && m.CenterId == centerId select r).SingleOrDefaultAsync(ct)
            ?? throw FlowException.NotFound("Không tìm thấy yêu cầu hỗ trợ tại trung tâm này.");
        request.ResolvedAt = body.Status == "Resolved" ? request.ResolvedAt ?? DateTime.UtcNow : null;
        request.Status = body.Status;
        request.HandledBy = actorId;
        if (!string.IsNullOrWhiteSpace(body.Note)) unitOfWork.Context.SupportRequestMessages.Add(new SupportRequestMessage {
            RequestId = id, SenderId = actorId, Message = body.Note.Trim(), CreatedAt = DateTime.UtcNow });
        Audit(actorId, centerId, request, "Support.Updated");
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await Get(actorId, centerId, id, ct);
    }

    private void Audit(long actorId, long centerId, SupportRequest request, string action) => unitOfWork.Context.AuditLogs.Add(new AuditLog {
        UserId = actorId, CenterId = centerId, EntityId = request.Id, EntityType = "SupportRequest", Action = action,
        NewValues = JsonSerializer.Serialize(new { request.MemberId, request.Status, request.Priority }), CreatedAt = DateTime.UtcNow });
}

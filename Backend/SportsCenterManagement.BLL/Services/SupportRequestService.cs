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
        if (!await StaffRecipients(centerId).AnyAsync(id => id == actorId, ct))
            throw new FlowException(403, "Bạn không có quyền tiếp nhận yêu cầu tại trung tâm này.");
    }

    private IQueryable<long> StaffRecipients(long centerId) =>
        from staff in unitOfWork.Context.StaffProfiles
        join user in unitOfWork.Context.Users on staff.UserId equals user.Id
        join role in unitOfWork.Context.Roles on user.RoleId equals role.Id
        join center in unitOfWork.Context.Centers on staff.CenterId equals center.Id
        where staff.CenterId == centerId && staff.Status == "Active" && user.Status == "Active"
            && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow)
            && center.Status == "Active" && (role.Name == "Receptionist" || role.Name == "Manager")
        select user.Id;

    private async Task<(long CenterId, long? MemberId)> Requester(long actorId, CancellationToken ct)
    {
        var role = await (from user in unitOfWork.Context.Users
            join accountRole in unitOfWork.Context.Roles on user.RoleId equals accountRole.Id
            where user.Id == actorId && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow)
            select accountRole.Name).SingleOrDefaultAsync(ct);
        long? centerId = null;
        long? memberId = null;
        if (role == "Member")
        {
            var member = await unitOfWork.Context.MemberProfiles.Where(item => item.UserId == actorId)
                .Select(item => new { item.Id, item.CenterId }).SingleOrDefaultAsync(ct);
            centerId = member?.CenterId;
            memberId = member?.Id;
        }
        else if (role == "Coach")
        {
            centerId = await unitOfWork.Context.CoachProfiles
                .Where(item => item.UserId == actorId && item.Status == "Active")
                .Select(item => (long?)item.CenterId).SingleOrDefaultAsync(ct);
        }
        if (!centerId.HasValue || !await unitOfWork.Context.Centers.AnyAsync(item => item.Id == centerId && item.Status == "Active", ct))
            throw new FlowException(403, "Tài khoản cần là Member hoặc Coach đang hoạt động và được gán trung tâm để gửi yêu cầu hỗ trợ.");
        return (centerId.Value, memberId);
    }

    private IQueryable<SupportRequest> Requests(long centerId) => unitOfWork.Context.SupportRequests.AsNoTracking()
        .Where(request => request.CenterId == centerId);

    private IQueryable<SupportSummary> Summaries(IQueryable<SupportRequest> requests) =>
        from request in requests
        join user in unitOfWork.Context.Users.AsNoTracking() on request.RequesterId equals user.Id
        from member in unitOfWork.Context.MemberProfiles.AsNoTracking().Where(item => item.Id == request.MemberId).DefaultIfEmpty()
        orderby request.CreatedAt descending, request.Id descending
        select new SupportSummary(request.Id, request.MemberId, member == null ? null : member.FullName,
            member == null ? null : member.MemberCode, request.Subject, request.Status,
            request.CreatedAt, request.ResolvedAt, request.RequesterId,
            member != null ? member.FullName : unitOfWork.Context.CoachProfiles.Where(item => item.UserId == request.RequesterId)
                .Select(item => item.FullName).FirstOrDefault() ?? user.Username,
            request.MemberId.HasValue ? "MEMBER" : "COACH", request.CenterId);

    private static void Validate(string subject, string description)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description)
            || subject.Length > 200 || description.Length > 5000)
            throw new FlowException(400, "Nhập tiêu đề và nội dung yêu cầu hợp lệ.");
    }

    private async Task<SupportPage> Page(IQueryable<SupportRequest> rows, string? query, string? status,
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100 || query?.Length > 200)
            throw new FlowException(400, "Bộ lọc hoặc phân trang không hợp lệ.");
        if (!string.IsNullOrEmpty(status) && status is not ("Open" or "InProgress" or "Resolved"))
            throw new FlowException(400, "Trạng thái yêu cầu không hợp lệ.");
        var term = query?.Trim();
        if (!string.IsNullOrEmpty(term)) rows = rows.Where(request => request.Subject.Contains(term)
            || unitOfWork.Context.MemberProfiles.Any(member => member.Id == request.MemberId
                && (member.FullName.Contains(term) || member.MemberCode.Contains(term)))
            || unitOfWork.Context.CoachProfiles.Any(coach => coach.UserId == request.RequesterId
                && (coach.FullName.Contains(term) || coach.CoachCode.Contains(term))));
        if (!string.IsNullOrEmpty(status)) rows = rows.Where(request => request.Status == status);
        var count = await rows.CountAsync(ct);
        var items = await Summaries(rows.OrderByDescending(request => request.CreatedAt).ThenByDescending(request => request.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new SupportPage(items, count, page, pageSize);
    }

    private async Task<SupportDetails> Details(IQueryable<SupportRequest> rows, long id, CancellationToken ct)
    {
        var summary = await Summaries(rows.Where(request => request.Id == id)).SingleOrDefaultAsync(ct)
            ?? throw new FlowException(404, "Không tìm thấy yêu cầu hỗ trợ.");
        var description = await rows.Where(request => request.Id == id).Select(request => request.Description).SingleAsync(ct);
        var messages = await (from message in unitOfWork.Context.SupportRequestMessages.AsNoTracking()
            join user in unitOfWork.Context.Users.AsNoTracking() on message.SenderId equals user.Id
            where message.RequestId == id orderby message.CreatedAt, message.Id
            select new SupportMessage(message.Id, user.Username, message.Message, message.CreatedAt)).ToListAsync(ct);
        return new SupportDetails(summary, description, messages);
    }

    public async Task<SupportPage> List(long actorId, long centerId, string? query, string? status, int page, int pageSize, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        return await Page(Requests(centerId), query, status, page, pageSize, ct);
    }

    public async Task<SupportDetails> Get(long actorId, long centerId, long id, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        return await Details(Requests(centerId), id, ct);
    }

    public async Task<SupportPage> ListMine(long actorId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var requester = await Requester(actorId, ct);
        return await Page(Requests(requester.CenterId).Where(request => request.RequesterId == actorId), null, status, page, pageSize, ct);
    }

    public async Task<SupportDetails> GetMine(long actorId, long id, CancellationToken ct)
    {
        var requester = await Requester(actorId, ct);
        return await Details(Requests(requester.CenterId).Where(request => request.RequesterId == actorId), id, ct);
    }

    public async Task<SupportDetails> CreateMine(long actorId, SubmitSupportRequest body, CancellationToken ct)
    {
        var requester = await Requester(actorId, ct);
        Validate(body.Subject, body.Description);
        await using var tx = await unitOfWork.Context.Database.BeginTransactionAsync(ct);
        var request = NewRequest(actorId, requester.CenterId, requester.MemberId, body.Subject, body.Description);
        unitOfWork.Context.SupportRequests.Add(request);
        await unitOfWork.SaveChangesAsync(ct);
        Audit(actorId, request, "Support.Submitted");
        await NotifyStaff(actorId, request, "Có yêu cầu hỗ trợ mới", ct);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetMine(actorId, request.Id, ct);
    }

    public async Task<SupportDetails> Create(long actorId, long centerId, CreateSupportRequest body, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        Validate(body.Subject, body.Description);
        var requesterId = await unitOfWork.Context.MemberProfiles.Where(member => member.Id == body.MemberId && member.CenterId == centerId)
            .Select(member => (long?)member.UserId).SingleOrDefaultAsync(ct)
            ?? throw new FlowException(404, "Không tìm thấy thành viên tại trung tâm này.");
        await using var tx = await unitOfWork.Context.Database.BeginTransactionAsync(ct);
        var request = NewRequest(requesterId, centerId, body.MemberId, body.Subject, body.Description);
        request.HandledBy = actorId;
        unitOfWork.Context.SupportRequests.Add(request);
        await unitOfWork.SaveChangesAsync(ct);
        Audit(actorId, request, "Support.Created");
        await FlowNotificationWriter.CreateAsync(unitOfWork.Context, actorId, [requesterId],
            "Yêu cầu hỗ trợ đã được tiếp nhận", $"Trung tâm đã tiếp nhận yêu cầu: {request.Subject}.", "SupportUpdated", ct);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await Get(actorId, centerId, request.Id, ct);
    }

    public async Task<SupportDetails> Update(long actorId, long centerId, long id, UpdateSupportRequest body, CancellationToken ct)
    {
        await EnsureStaff(actorId, centerId, ct);
        if (body.Status is not ("Open" or "InProgress" or "Resolved") || body.Note?.Length > 5000)
            throw new FlowException(400, "Trạng thái hoặc ghi chú không hợp lệ.");
        await using var tx = await unitOfWork.Context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var request = await unitOfWork.Context.SupportRequests.SingleOrDefaultAsync(item => item.Id == id && item.CenterId == centerId, ct)
            ?? throw new FlowException(404, "Không tìm thấy yêu cầu hỗ trợ tại trung tâm này.");
        var statusChanged = request.Status != body.Status;
        request.ResolvedAt = body.Status == "Resolved" ? request.ResolvedAt ?? DateTime.UtcNow : null;
        request.Status = body.Status;
        request.HandledBy = actorId;
        if (!string.IsNullOrWhiteSpace(body.Note)) AddMessage(actorId, id, body.Note);
        Audit(actorId, request, "Support.Updated");
        if (statusChanged || !string.IsNullOrWhiteSpace(body.Note))
        {
            var statusLabel = body.Status switch { "Resolved" => "Đã giải quyết", "InProgress" => "Đang xử lý", _ => "Đã tiếp nhận" };
            await FlowNotificationWriter.CreateAsync(unitOfWork.Context, actorId, [request.RequesterId],
                "Yêu cầu hỗ trợ được cập nhật", $"{request.Subject}: {statusLabel}." +
                (string.IsNullOrWhiteSpace(body.Note) ? "" : $"\nPhản hồi: {body.Note.Trim()}"), "SupportUpdated", ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await Get(actorId, centerId, id, ct);
    }

    public async Task<SupportDetails> ReplyMine(long actorId, long id, ReplyToSupportRequest body, CancellationToken ct)
    {
        var requester = await Requester(actorId, ct);
        if (string.IsNullOrWhiteSpace(body.Message) || body.Message.Length > 5000)
            throw new FlowException(400, "Nhập nội dung phản hồi từ 1 đến 5000 ký tự.");
        await using var tx = await unitOfWork.Context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var request = await unitOfWork.Context.SupportRequests.SingleOrDefaultAsync(item => item.Id == id
            && item.RequesterId == actorId && item.CenterId == requester.CenterId, ct)
            ?? throw new FlowException(404, "Không tìm thấy yêu cầu hỗ trợ.");
        if (request.Status == "Resolved") throw new FlowException(409, "Yêu cầu đã giải quyết. Hãy tạo yêu cầu mới nếu cần hỗ trợ thêm.");
        AddMessage(actorId, id, body.Message);
        Audit(actorId, request, "Support.Replied");
        await NotifyStaff(actorId, request, "Yêu cầu hỗ trợ có phản hồi mới", ct);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetMine(actorId, id, ct);
    }

    private static SupportRequest NewRequest(long requesterId, long centerId, long? memberId, string subject, string description) =>
        new() { RequesterId = requesterId, CenterId = centerId, MemberId = memberId, Subject = subject.Trim(),
            Description = description.Trim(), Status = "Open", CreatedAt = DateTime.UtcNow };

    private void AddMessage(long actorId, long requestId, string message) =>
        unitOfWork.Context.SupportRequestMessages.Add(new SupportRequestMessage {
            RequestId = requestId, SenderId = actorId, Message = message.Trim(), CreatedAt = DateTime.UtcNow });

    private async Task NotifyStaff(long actorId, SupportRequest request, string title, CancellationToken ct) =>
        await FlowNotificationWriter.CreateAsync(unitOfWork.Context, actorId, await StaffRecipients(request.CenterId).Distinct().ToListAsync(ct),
            title, $"Yêu cầu #{request.Id}: {request.Subject}.", "SupportSubmitted", ct);

    private void Audit(long actorId, SupportRequest request, string action) => unitOfWork.Context.AuditLogs.Add(new AuditLog {
        UserId = actorId, CenterId = request.CenterId, EntityId = request.Id, EntityType = "SupportRequest", Action = action,
        NewValues = JsonSerializer.Serialize(new { request.RequesterId, request.MemberId, request.Status }), CreatedAt = DateTime.UtcNow });
}

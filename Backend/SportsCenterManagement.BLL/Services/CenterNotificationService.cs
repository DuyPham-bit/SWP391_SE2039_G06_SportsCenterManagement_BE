using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Notifications;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CenterNotificationService(IUnitOfWork unitOfWork) : ICenterNotificationService
{
    private static readonly string[] AllowedRoles =
        [RoleNames.Member, RoleNames.Coach, RoleNames.Receptionist, RoleNames.Manager];

    public async Task<SendCenterNotificationResponse> SendAsync(
        long actorUserId,
        long centerId,
        SendCenterNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actorUserId, centerId, cancellationToken);
        var title = request.Title?.Trim();
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200
            || string.IsNullOrWhiteSpace(message) || message.Length > 5000)
            throw new FlowException(400, "Tiêu đề hoặc nội dung thông báo không hợp lệ.");

        var requestedRoles = (request.TargetRoles ?? [])
            .Select(role => AllowedRoles.FirstOrDefault(allowed => allowed.Equals(role?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(role => role is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!request.SendToAll && requestedRoles.Length == 0)
            throw new FlowException(400, "Chọn ít nhất một vai trò nhận thông báo.");
        if ((request.TargetRoles ?? []).Any(role => !AllowedRoles.Any(allowed => allowed.Equals(role?.Trim(), StringComparison.OrdinalIgnoreCase))))
            throw new FlowException(400, "Vai trò nhận thông báo không hợp lệ.");

        var targetRoles = request.SendToAll ? AllowedRoles : requestedRoles;
        var recipientIds = await (from user in unitOfWork.Context.Users.AsNoTracking()
            join role in unitOfWork.Context.Roles.AsNoTracking() on user.RoleId equals role.Id
            where user.Id != actorUserId && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow)
                && targetRoles.Contains(role.Name)
                && ((role.Name == RoleNames.Member && unitOfWork.Context.MemberProfiles.Any(profile => profile.UserId == user.Id && profile.CenterId == centerId))
                    || (role.Name == RoleNames.Coach && unitOfWork.Context.CoachProfiles.Any(profile => profile.UserId == user.Id && profile.CenterId == centerId && profile.Status == "Active"))
                    || ((role.Name == RoleNames.Receptionist || role.Name == RoleNames.Manager)
                        && unitOfWork.Context.StaffProfiles.Any(profile => profile.UserId == user.Id && profile.CenterId == centerId && profile.Status == "Active")))
            select user.Id).Distinct().ToListAsync(cancellationToken);

        if (recipientIds.Count == 0)
            throw new FlowException(409, "Không có tài khoản đang hoạt động phù hợp với nhóm người nhận đã chọn.");

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(cancellationToken);
        var notification = await FlowNotificationWriter.CreateWithResultAsync(
            unitOfWork.Context, actorUserId, recipientIds, title, message, "CenterAnnouncement", cancellationToken)
            ?? throw new FlowException(409, "Không thể xác định người nhận thông báo.");

        var allowedChannels = new[] { "InApp", "Email" };
        var channels = (request.Channels ?? ["InApp", "Email"])
            .Where(x => allowedChannels.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(x => allowedChannels.First(a => a.Equals(x, StringComparison.OrdinalIgnoreCase)))
            .Distinct().ToArray();
        if (channels.Length == 0) channels = ["InApp"];

        unitOfWork.Context.CommunicationDeliveries.AddRange(recipientIds.SelectMany(id => channels.Select(channel => new CommunicationDelivery
        {
            NotificationId = notification.Id,
            RecipientUserId = id,
            Channel = channel,
            Status = channel == "InApp" ? "Delivered" : "Queued",
            ProviderMessage = channel == "InApp" ? "Đã giao trong ứng dụng" : "Đang chờ cấu hình cổng gửi bên ngoài",
            CreatedAt = notification.CreatedAt,
            SentAt = channel == "InApp" ? notification.CreatedAt : null
        })));
        unitOfWork.Context.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            CenterId = centerId,
            Action = "CENTER_NOTIFICATION_SENT",
            EntityType = "Notification",
            EntityId = notification.Id,
            NewValues = JsonSerializer.Serialize(new { TargetRoles = targetRoles, RecipientCount = recipientIds.Count }),
            CreatedAt = DateTime.UtcNow
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SendCenterNotificationResponse(
            notification.Id, recipientIds.Count, targetRoles, notification.CreatedAt);
    }

    private async Task EnsureManagerAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        var permitted = await (from user in unitOfWork.Context.Users.AsNoTracking()
            join role in unitOfWork.Context.Roles.AsNoTracking() on user.RoleId equals role.Id
            join staff in unitOfWork.Context.StaffProfiles.AsNoTracking() on user.Id equals staff.UserId
            join center in unitOfWork.Context.Centers.AsNoTracking() on staff.CenterId equals center.Id
            where user.Id == actorUserId && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow)
                && role.Name == RoleNames.Manager && staff.CenterId == centerId
                && staff.Status == "Active" && center.Status == "Active"
            select user.Id).AnyAsync(cancellationToken);
        if (!permitted)
            throw new FlowException(403, "Bạn không có quyền gửi thông báo tại trung tâm này.");
    }
}

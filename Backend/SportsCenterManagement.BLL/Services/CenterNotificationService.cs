using System.Text.Json;
using SportsCenterManagement.BLL.DTOs.Notifications;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CenterNotificationService(
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : ICenterNotificationService
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
        var recipientIds = await notificationRepository.GetRecipientUserIdsForCenterAsync(
            actorUserId, centerId, targetRoles, cancellationToken);

        if (recipientIds.Count == 0)
            throw new FlowException(409, "Không có tài khoản đang hoạt động phù hợp với nhóm người nhận đã chọn.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var notification = await notificationRepository.CreateNotificationWithRecipientsAsync(
            actorUserId, recipientIds, title, message, "CenterAnnouncement", cancellationToken)
            ?? throw new FlowException(409, "Không thể xác định người nhận thông báo.");

        var allowedChannels = new[] { "InApp", "Email" };
        var channels = (request.Channels ?? ["InApp", "Email"])
            .Where(x => allowedChannels.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(x => allowedChannels.First(a => a.Equals(x, StringComparison.OrdinalIgnoreCase)))
            .Distinct().ToArray();
        if (channels.Length == 0) channels = ["InApp"];

        var deliveryRepo = unitOfWork.Repository<CommunicationDelivery>();
        foreach (var id in recipientIds)
        {
            foreach (var channel in channels)
            {
                await deliveryRepo.AddAsync(new CommunicationDelivery
                {
                    NotificationId = notification.Id,
                    RecipientUserId = id,
                    Channel = channel,
                    Status = channel == "InApp" ? "Delivered" : "Queued",
                    ProviderMessage = channel == "InApp" ? "Đã giao trong ứng dụng" : "Đang chờ cấu hình cổng gửi bên ngoài",
                    CreatedAt = notification.CreatedAt,
                    SentAt = channel == "InApp" ? notification.CreatedAt : null
                }, cancellationToken);
            }
        }

        var auditRepo = unitOfWork.Repository<AuditLog>();
        await auditRepo.AddAsync(new AuditLog
        {
            UserId = actorUserId,
            CenterId = centerId,
            Action = "CENTER_NOTIFICATION_SENT",
            EntityType = "Notification",
            EntityId = notification.Id,
            NewValues = JsonSerializer.Serialize(new { TargetRoles = targetRoles, RecipientCount = recipientIds.Count }),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SendCenterNotificationResponse(
            notification.Id, recipientIds.Count, targetRoles, notification.CreatedAt);
    }

    public async Task<IReadOnlyList<UserNotificationResponse>> GetMineAsync(long userId, CancellationToken ct = default)
    {
        var items = await notificationRepository.GetUserNotificationsAsync(userId, 200, ct);
        return items.Select(n => new UserNotificationResponse(
            n.Id, n.Title, n.Content, n.NotificationType, n.SenderId, n.CreatedAt, n.IsRead)).ToList();
    }

    public async Task<bool> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default)
    {
        return await notificationRepository.MarkAsReadAsync(notificationId, userId, ct);
    }

    public async Task MarkAllAsReadAsync(long userId, CancellationToken ct = default)
    {
        await notificationRepository.MarkAllAsReadAsync(userId, ct);
    }

    public async Task<IReadOnlyList<CoachSentNotificationResponse>> GetCoachSentNotificationsAsync(long coachUserId, CancellationToken ct = default)
    {
        var rows = await notificationRepository.GetCoachSentNotificationsAsync(coachUserId, 200, ct);
        var targetIds = rows.SelectMany(row => ParseClassIds(row.TargetClassIds)).Distinct().ToArray();
        var classNames = await notificationRepository.GetClassNamesAsync(targetIds, ct);

        return rows.Select(row =>
        {
            var classIds = ParseClassIds(row.TargetClassIds);
            var target = classIds.Length > 0
                ? "Lớp: " + string.Join(", ", classIds.Select(id => classNames.GetValueOrDefault(id, $"#{id}")))
                : row.SingleMemberName is not null ? "Học viên: " + row.SingleMemberName : "Học viên được phân công";
            return new CoachSentNotificationResponse(
                row.Id,
                row.Title,
                row.Content,
                row.CreatedAt,
                row.RecipientCount,
                classIds.Length == 1 ? (long?)classIds[0] : null,
                classIds,
                target);
        }).ToList();
    }

    private async Task EnsureManagerAsync(long actorUserId, long centerId, CancellationToken cancellationToken)
    {
        var permitted = await notificationRepository.IsActiveManagerAtCenterAsync(actorUserId, centerId, cancellationToken);
        if (!permitted)
            throw new FlowException(403, "Bạn không có quyền gửi thông báo tại trung tâm này.");
    }

    private static long[] ParseClassIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return (JsonSerializer.Deserialize<long[]>(value) ?? []).Where(id => id > 0).Distinct().ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

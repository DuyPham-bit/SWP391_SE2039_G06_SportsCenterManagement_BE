using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.BackgroundServices;

/// <summary>
/// Delivers queued Email/SMS/Push notifications to configurable provider webhooks.
/// The provider is responsible for translating the normalized payload to its vendor API.
/// </summary>
public sealed class CommunicationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<CommunicationDeliveryWorker> logger) : BackgroundService
{
    private static readonly string[] ExternalChannels = ["Email"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = Math.Clamp(
            configuration.GetValue<int?>("ExternalCommunications:PollSeconds") ?? 15,
            5,
            300);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds), timeProvider);

        try
        {
            do
            {
                try
                {
                    await DispatchBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Could not dispatch external communications; retrying on the next tick.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var endpoints = ExternalChannels
            .Select(channel => new
            {
                Channel = channel,
                Url = configuration[$"ExternalCommunications:{channel}:WebhookUrl"]
            })
            .Where(item => Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .ToDictionary(item => item.Channel, item => item.Url!, StringComparer.OrdinalIgnoreCase);

        // Queued deliveries are intentionally retained until an administrator configures a gateway.
        if (endpoints.Count == 0) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
        var enabledChannels = endpoints.Keys.ToArray();
        var deliveries = await db.CommunicationDeliveries
            .Where(item => item.Status == "Queued" && enabledChannels.Contains(item.Channel))
            .OrderBy(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (deliveries.Count == 0) return;

        var notificationIds = deliveries
            .Where(item => item.NotificationId.HasValue)
            .Select(item => item.NotificationId!.Value)
            .Distinct()
            .ToArray();
        var recipientIds = deliveries.Select(item => item.RecipientUserId).Distinct().ToArray();
        var notifications = await db.Notifications.AsNoTracking()
            .Where(item => notificationIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var recipients = await db.Users.AsNoTracking()
            .Where(item => recipientIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var client = httpClientFactory.CreateClient(nameof(CommunicationDeliveryWorker));

        foreach (var delivery in deliveries)
        {
            if (!delivery.NotificationId.HasValue
                || !notifications.TryGetValue(delivery.NotificationId.Value, out var notification)
                || !recipients.TryGetValue(delivery.RecipientUserId, out var recipient))
            {
                MarkFailed(delivery, "Không tìm thấy thông báo hoặc người nhận.");
                continue;
            }

            var destination = delivery.Channel switch
            {
                "Email" => recipient.Email,
                "SMS" => recipient.Phone,
                "Push" => recipient.Id.ToString(),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(destination))
            {
                MarkFailed(delivery, $"Người nhận chưa có địa chỉ {delivery.Channel}.");
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoints[delivery.Channel]);
            var token = configuration[$"ExternalCommunications:{delivery.Channel}:BearerToken"];
            if (string.IsNullOrWhiteSpace(token))
                token = configuration["ExternalCommunications:BearerToken"];
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            request.Content = JsonContent.Create(new
            {
                deliveryId = delivery.Id,
                channel = delivery.Channel,
                recipientUserId = recipient.Id,
                to = destination,
                title = notification.Title,
                message = notification.Message,
                notificationType = notification.NotificationType,
                createdAt = notification.CreatedAt
            });

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                var providerMessage = await response.Content.ReadAsStringAsync(cancellationToken);
                providerMessage = Truncate(providerMessage, 900);
                if (response.IsSuccessStatusCode)
                {
                    delivery.Status = "Sent";
                    delivery.SentAt = timeProvider.GetUtcNow().UtcDateTime;
                    delivery.ProviderMessage = string.IsNullOrWhiteSpace(providerMessage)
                        ? $"Gateway accepted ({(int)response.StatusCode})."
                        : providerMessage;
                }
                else
                {
                    MarkFailed(delivery,
                        $"Gateway trả về {(int)response.StatusCode}: {providerMessage}".TrimEnd());
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                MarkFailed(delivery, $"Không gọi được gateway: {exception.Message}");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Processed {Count} queued external communication deliveries.", deliveries.Count);
    }

    private static void MarkFailed(DAL.Entities.CommunicationDelivery delivery, string message)
    {
        delivery.Status = "Failed";
        delivery.ProviderMessage = Truncate(message, 1000);
    }

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value ?? string.Empty
            : value[..maxLength];
}

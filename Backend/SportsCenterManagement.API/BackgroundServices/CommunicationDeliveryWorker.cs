using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.BackgroundServices;

/// <summary>
/// Delivers queued Email/SMS/Push notifications to configurable provider webhooks.
/// Supports multi-instance distributed lease claiming, exponential backoff retries,
/// strict HTTPS enforcement in production, and delivery idempotency keys.
/// </summary>
public sealed class CommunicationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<CommunicationDeliveryWorker> logger) : BackgroundService
{
    private static readonly string[] ExternalChannels = ["Email"];
    private readonly string _workerInstanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

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
        var isProduction = environment.IsProduction();
        var endpoints = ExternalChannels
            .Select(channel => new
            {
                Channel = channel,
                Url = configuration[$"ExternalCommunications:{channel}:WebhookUrl"]
            })
            .Where(item =>
            {
                if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)) return false;
                if (uri.Scheme == Uri.UriSchemeHttps) return true;
                // Allow non-HTTPS strictly on loopback / localhost when NOT in production
                return !isProduction && uri.IsLoopback;
            })
            .ToDictionary(item => item.Channel, item => item.Url!, StringComparer.OrdinalIgnoreCase);

        // Queued deliveries are intentionally retained until an administrator configures a gateway.
        if (endpoints.Count == 0) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
        var enabledChannels = endpoints.Keys.ToArray();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leaseUntil = now.AddMinutes(2);

        // Atomic claim: fetch candidates that are Queued or have an expired lease, and whose retry time has arrived
        var deliveries = await db.CommunicationDeliveries
            .Where(item =>
                (item.Status == "Queued" || (item.Status == "Processing" && item.LeaseUntil < now))
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now)
                && enabledChannels.Contains(item.Channel))
            .OrderBy(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (deliveries.Count == 0) return;

        // Claim ownership immediately before dispatching to prevent other instances from processing concurrently
        foreach (var delivery in deliveries)
        {
            delivery.Status = "Processing";
            delivery.LockedBy = _workerInstanceId;
            delivery.LeaseUntil = leaseUntil;
            delivery.AttemptCount++;
        }
        await db.SaveChangesAsync(cancellationToken);

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
            // Attach idempotency key header so providers do not send duplicate messages on retries
            request.Headers.Add("X-Delivery-Id", delivery.Id.ToString());

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
                    delivery.LockedBy = null;
                    delivery.LeaseUntil = null;
                    delivery.LastError = null;
                    delivery.ProviderMessage = string.IsNullOrWhiteSpace(providerMessage)
                        ? $"Gateway accepted ({(int)response.StatusCode})."
                        : providerMessage;
                }
                else
                {
                    HandleRetryOrFailure(delivery,
                        $"Gateway trả về {(int)response.StatusCode}: {providerMessage}".TrimEnd(), now);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                HandleRetryOrFailure(delivery, $"Không gọi được gateway: {exception.Message}", now);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Processed {Count} external communication deliveries by worker {WorkerId}.", deliveries.Count, _workerInstanceId);
    }

    private void HandleRetryOrFailure(DAL.Entities.CommunicationDelivery delivery, string message, DateTime nowUtc)
    {
        delivery.LastError = Truncate(message, 1000);
        delivery.ProviderMessage = Truncate(message, 1000);
        delivery.LockedBy = null;
        delivery.LeaseUntil = null;

        if (delivery.AttemptCount < 5)
        {
            delivery.Status = "Queued";
            // Exponential backoff: 1m, 2m, 4m, 8m, 16m
            var delayMinutes = Math.Pow(2, Math.Min(delivery.AttemptCount - 1, 4));
            delivery.NextAttemptAt = nowUtc.AddMinutes(delayMinutes);
            logger.LogWarning("Delivery {Id} ({Channel}) attempt {Attempt} failed: {Error}. Retrying in {Minutes}m.",
                delivery.Id, delivery.Channel, delivery.AttemptCount, delivery.LastError, delayMinutes);
        }
        else
        {
            delivery.Status = "Failed";
            logger.LogError("Delivery {Id} ({Channel}) failed permanently after {Attempt} attempts: {Error}",
                delivery.Id, delivery.Channel, delivery.AttemptCount, delivery.LastError);
        }
    }

    private static void MarkFailed(DAL.Entities.CommunicationDelivery delivery, string message)
    {
        delivery.Status = "Failed";
        delivery.ProviderMessage = Truncate(message, 1000);
        delivery.LastError = Truncate(message, 1000);
        delivery.LockedBy = null;
        delivery.LeaseUntil = null;
        delivery.NextAttemptAt = null;
    }

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value ?? string.Empty
            : value[..maxLength];
}

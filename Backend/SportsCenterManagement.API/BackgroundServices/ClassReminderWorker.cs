using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.BackgroundServices;

public sealed class ClassReminderWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ClassReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), timeProvider);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<ClassReminderService>();
                    var sent = await service.SendDueRemindersAsync(stoppingToken);
                    if (sent > 0) logger.LogInformation("Created {Count} class reminders.", sent);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // A fresh scope retries unsent reminders after transient failures.
                    logger.LogError(exception, "Could not create class reminders; retrying on the next tick.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

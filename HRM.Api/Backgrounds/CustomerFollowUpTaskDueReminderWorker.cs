using HRM.Application.Features.CRM.CustomerCare.Services;

namespace HRM.Api.Backgrounds;

/// <summary>
/// Kích hoạt processor cảnh báo follow-up task đến hạn; rule nghiệp vụ nằm trong Application.
/// </summary>
public sealed class CustomerFollowUpTaskDueReminderWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CustomerFollowUpTaskDueReminderWorker> _logger;

    public CustomerFollowUpTaskDueReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CustomerFollowUpTaskDueReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<ICustomerFollowUpTaskDueReminderProcessor>();

                var processedCount = await processor.ProcessDueRemindersAsync(stoppingToken);
                if (processedCount > 0)
                {
                    _logger.LogInformation(
                        "Published {ReminderCount} customer follow-up task due reminders.",
                        processedCount);
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer follow-up task due reminder worker failed.");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}

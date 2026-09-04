using HRM.Application.Features.CRM.Quotations.Services;

namespace HRM.Api.Backgrounds.CRM.Quotations;

/// <summary>
/// Reconcile báo giá chờ duyệt và xử lý giá chuẩn hết hạn; rule và recipient nằm ở Application.
/// </summary>
public sealed class QuotationPricingExpiryReminderWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuotationPricingExpiryReminderWorker> _logger;

    public QuotationPricingExpiryReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<QuotationPricingExpiryReminderWorker> logger)
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
                    .GetRequiredService<IQuotationPricingExpiryReminderProcessor>();
                var processedCount = await processor.ProcessExpiredPricingAsync(stoppingToken);
                if (processedCount > 0)
                {
                    _logger.LogInformation(
                        "Processed {LifecycleChangeCount} quotation pricing lifecycle changes.",
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
                _logger.LogError(ex, "Quotation pricing expiry reminder worker failed.");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}

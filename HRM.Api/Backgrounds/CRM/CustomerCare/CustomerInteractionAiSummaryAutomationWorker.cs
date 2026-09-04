using HRM.Application.Features.CRM.InteractionSummaries.Services.Automation;
using HRM.Application.Abstractions.Commons.Time;
using Microsoft.Extensions.Options;

namespace HRM.Api.Backgrounds.CRM.CustomerCare;

/// <summary>
/// Kích hoạt quét AI summary CRM theo chu kỳ cấu hình. Rule chọn customer, cache và gọi AI
/// nằm trong Application processor để worker không truy cập trực tiếp dữ liệu nghiệp vụ.
/// </summary>
public sealed class CustomerInteractionAiSummaryAutomationWorker : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CustomerInteractionAiSummaryAutomationOptions _options;
    private readonly ILogger<CustomerInteractionAiSummaryAutomationWorker> _logger;

    public CustomerInteractionAiSummaryAutomationWorker(
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider dateTimeProvider,
        IOptions<CustomerInteractionAiSummaryAutomationOptions> options,
        ILogger<CustomerInteractionAiSummaryAutomationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("CRM AI summary automation is disabled.");
            return;
        }

        var pollInterval = TimeSpan.FromMinutes(Math.Clamp(_options.PollMinutes, 1, 1_440));
        _logger.LogInformation(
            "CRM AI summary automation is enabled. Poll interval={PollIntervalMinutes} minutes, scanLimit={ScanLimit}, maxAiRequestsPerRun={MaxAiRequestsPerRun}, maxCustomersPerAiRequest={MaxCustomersPerAiRequest}, monthlyDays={MonthlyRunStartDay}-{MonthlyRunEndDay}, yearlyDays={YearlyRunStartDay}-{YearlyRunEndDay} in January, lifetimeDays={LifetimeRunStartDay}-{LifetimeRunEndDay}.",
            pollInterval.TotalMinutes,
            _options.ScanCustomerLimit,
            _options.MaxAiRequestsPerRun,
            _options.MaxCustomersPerAiRequest,
            _options.MonthlyRunStartDay,
            _options.MonthlyRunEndDay,
            _options.YearlyRunStartDay,
            _options.YearlyRunEndDay,
            _options.LifetimeRunStartDay,
            _options.LifetimeRunEndDay);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _dateTimeProvider.Now;
                if (!TryCreateRunPlan(now, out var runPlan))
                {
                    _logger.LogInformation(
                        "CRM AI summary automation skipped because {Date:yyyy-MM-dd} is outside all configured monthly, yearly and Lifetime windows.",
                        now);
                    await Task.Delay(pollInterval, stoppingToken);
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<ICustomerInteractionAiSummaryAutomationProcessor>();
                var result = await processor.ProcessAsync(
                    _options.ScanCustomerLimit,
                    _options.MaxAiRequestsPerRun,
                    _options.MaxCustomersPerAiRequest,
                    runPlan,
                    stoppingToken);

                _logger.LogInformation(
                    "CRM AI summary automation processed {RunPlan}: scanned {ScannedCount} summaries, requested {RequestCount}, succeeded {SuccessCount}, failed {FailedCount}, deferred {DeferredCount}, rateLimitReached={RateLimitReached}, requestLimitReached={RequestLimitReached}, publishedNotifications={PublishedNotificationCount}.",
                    DescribeRunPlan(runPlan),
                    result.ScannedSummaryCount,
                    result.AiRequestCount,
                    result.SuccessCount,
                    result.FailedCount,
                    result.DeferredCount,
                    result.RateLimitReached,
                    result.RequestLimitReached,
                    result.PublishedNotificationCount);

                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "CRM AI summary automation worker failed.");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private bool TryCreateRunPlan(
        DateTime now,
        out CustomerInteractionAiSummaryAutomationRunPlan runPlan)
    {
        var previousMonth = now.AddMonths(-1);
        var priorMonthTarget = new CustomerInteractionAiSummaryAutomationTarget(
            previousMonth.Year,
            previousMonth.Month);
        var monthlyEnabled = IsWithinDayRange(
            now.Day,
            _options.MonthlyRunStartDay,
            _options.MonthlyRunEndDay);
        var yearlyEnabled = now.Month == 1 && IsWithinDayRange(
            now.Day,
            _options.YearlyRunStartDay,
            _options.YearlyRunEndDay);
        var lifetimeEnabled = IsWithinDayRange(
            now.Day,
            _options.LifetimeRunStartDay,
            _options.LifetimeRunEndDay);

        if (!monthlyEnabled && !yearlyEnabled && !lifetimeEnabled)
        {
            runPlan = default!;
            return false;
        }

        runPlan = new CustomerInteractionAiSummaryAutomationRunPlan(
            monthlyEnabled ? priorMonthTarget : null,
            yearlyEnabled ? now.Year - 1 : null,
            lifetimeEnabled ? priorMonthTarget : null);
        return true;
    }

    private static bool IsWithinDayRange(int day, int configuredStartDay, int configuredEndDay)
    {
        var startDay = Math.Clamp(configuredStartDay, 1, 28);
        var endDay = Math.Clamp(configuredEndDay, startDay, 28);
        return day >= startDay && day <= endDay;
    }

    private static string DescribeRunPlan(CustomerInteractionAiSummaryAutomationRunPlan runPlan)
        => $"monthly={(runPlan.MonthlyTarget is null ? "none" : $"{runPlan.MonthlyTarget.Year}-{runPlan.MonthlyTarget.Month:D2}")}, " +
           $"yearly={(runPlan.YearlyTargetYear?.ToString() ?? "none")}, " +
           $"lifetime={(runPlan.LifetimeTarget is null ? "none" : $"{runPlan.LifetimeTarget.Year}-{runPlan.LifetimeTarget.Month:D2}")}";
}

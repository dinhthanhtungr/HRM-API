using System.Text.Json;
using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.InteractionSummaries.Services.Automation;

/// <summary>
/// Processor nền tạo summary theo dependency: tháng từ interaction; năm và Lifetime từ summary tháng.
/// Mỗi summary nền không lọc sale và luôn tách theo CompanyId.
/// </summary>
internal sealed class CustomerInteractionAiSummaryAutomationProcessor
    : ICustomerInteractionAiSummaryAutomationProcessor
{
    private static readonly DateTime LifetimeStart = new(2000, 1, 1);
    private const int MaxFailedCustomerCodesInSummaryNotification = 20;

    private readonly ICRMReadDbContext _readDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGeminiRateLimitService _rateLimitService;
    private readonly ICustomerInteractionAiSummaryClient _client;
    private readonly CustomerInteractionAiSummaryGenerationService _generationService;
    private readonly INotificationService _notificationService;

    public CustomerInteractionAiSummaryAutomationProcessor(
        ICRMReadDbContext readDbContext,
        IDateTimeProvider dateTimeProvider,
        IGeminiRateLimitService rateLimitService,
        ICustomerInteractionAiSummaryClient client,
        CustomerInteractionAiSummaryGenerationService generationService,
        INotificationService notificationService)
    {
        _readDbContext = readDbContext;
        _dateTimeProvider = dateTimeProvider;
        _rateLimitService = rateLimitService;
        _client = client;
        _generationService = generationService;
        _notificationService = notificationService;
    }

    public async Task<CustomerInteractionAiSummaryAutomationResult> ProcessAsync(
        int scanCustomerLimit,
        int maxAiRequests,
        int maxCustomersPerAiRequest,
        CustomerInteractionAiSummaryAutomationRunPlan runPlan,
        CancellationToken cancellationToken = default)
    {
        scanCustomerLimit = Math.Clamp(scanCustomerLimit, 1, 5_000);
        maxAiRequests = Math.Clamp(maxAiRequests, 1, 100);
        maxCustomersPerAiRequest = Math.Clamp(maxCustomersPerAiRequest, 1, 5);
        ValidateTarget(runPlan.MonthlyTarget);
        ValidateTarget(runPlan.LifetimeTarget);
        if (runPlan.YearlyTargetYear.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(runPlan.YearlyTargetYear.Value, 2000);
        }

        var now = _dateTimeProvider.Now;
        var candidates = new Dictionary<AutomationCandidateKey, AutomationCandidate>();
        if (runPlan.MonthlyTarget is not null)
        {
            await AddMonthlyCandidatesAsync(candidates, now, runPlan.MonthlyTarget, scanCustomerLimit, cancellationToken);
        }

        if (runPlan.YearlyTargetYear.HasValue)
        {
            await AddYearlyCandidatesAsync(candidates, now, runPlan.YearlyTargetYear.Value, scanCustomerLimit, cancellationToken);
        }

        if (runPlan.LifetimeTarget is not null)
        {
            await AddLifetimeCandidatesAsync(candidates, now, runPlan.LifetimeTarget, scanCustomerLimit, cancellationToken);
        }

        var orderedCandidates = candidates.Values
            .OrderBy(x => x.SummaryScope)
            .ThenByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ThenByDescending(x => x.LatestSourceChangedAt)
            .ThenBy(x => x.CustomerId)
            .ToArray();
        var companyStatistics = new Dictionary<Guid, CompanyAutomationStatistics>();
        var aiRequestCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var deferredCount = 0;
        var scannedSummaryCount = 0;
        var rateLimitReached = false;
        var requestLimitReached = false;

        var remainingCandidates = orderedCandidates.ToList();
        while (remainingCandidates.Count > 0)
        {
            var candidate = remainingCandidates[0];
            if (candidate.SummaryScope == CustomerInteractionSummaryScope.Monthly)
            {
                // Batch không trộn công ty để tenant data và người nhận notification luôn rõ ràng.
                var batchCandidates = remainingCandidates
                    .Where(x =>
                        x.SummaryScope == CustomerInteractionSummaryScope.Monthly &&
                        x.CompanyId == candidate.CompanyId)
                    .Take(maxCustomersPerAiRequest)
                    .ToArray();
                foreach (var batchCandidate in batchCandidates)
                {
                    remainingCandidates.Remove(batchCandidate);
                }

                scannedSummaryCount += batchCandidates.Length;
                var batchOutcome = await _generationService.GenerateMonthlyBatchAsync(
                    batchCandidates
                        .Select(x => new CustomerInteractionAiSummaryGenerationBatchRequest(
                            x.CustomerId,
                            x.CompanyId,
                            x.AuditEmployeeId,
                            x.Year ?? now.Year,
                            x.Month ?? now.Month))
                        .ToArray(),
                    maxCustomersPerAiRequest,
                    cancellationToken);
                var outcomeByCustomerId = batchOutcome.Items
                    .GroupBy(x => x.Request.CustomerId)
                    .ToDictionary(x => x.Key, x => x.First().Outcome);
                var countedBatchRequest = false;
                foreach (var batchCandidate in batchCandidates)
                {
                    var outcome = outcomeByCustomerId.TryGetValue(batchCandidate.CustomerId, out var itemOutcome)
                        ? itemOutcome
                        : CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                            HRM.Application.Commons.Models.OperationResult<CustomerInteractionAiSummaryDto>.Fail(
                                "AI batch did not return an outcome for this customer."));
                    var statistics = GetOrCreateCompanyStatistics(companyStatistics, batchCandidate);
                    var countRequestForThisItem = outcome.AiRequested && !countedBatchRequest;
                    statistics.Record(batchCandidate.SummaryScope, outcome, countRequestForThisItem);
                    if (outcome.AiRequested)
                    {
                        countedBatchRequest = true;
                        if (outcome.Result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            failedCount++;
                            statistics.RecordFailure(batchCandidate);
                            await PublishFailureNotificationAsync(batchCandidate, outcome.Result.Message, cancellationToken);
                        }
                    }
                    else if (!outcome.Result.Success)
                    {
                        deferredCount++;
                    }
                }

                if (batchOutcome.AiRequested)
                {
                    aiRequestCount++;
                }
            }
            else
            {
                remainingCandidates.RemoveAt(0);
                scannedSummaryCount++;
                var statistics = GetOrCreateCompanyStatistics(companyStatistics, candidate);
                var outcome = await _generationService.GenerateAsync(
                    candidate.CustomerId,
                    candidate.CompanyId,
                    null,
                    candidate.AuditEmployeeId,
                    BuildRequest(candidate),
                    cancellationToken);
                statistics.Record(candidate.SummaryScope, outcome);

                if (outcome.AiRequested)
                {
                    aiRequestCount++;
                    if (outcome.Result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedCount++;
                        statistics.RecordFailure(candidate);
                        await PublishFailureNotificationAsync(candidate, outcome.Result.Message, cancellationToken);
                    }
                }
                else if (!outcome.Result.Success)
                {
                    deferredCount++;
                }
            }

            var rate = _rateLimitService.GetCurrent(_client.Model);
            if (!rate.CanRequest)
            {
                rateLimitReached = true;
                foreach (var companyStatistic in companyStatistics.Values)
                {
                    companyStatistic.RateLimitReached = true;
                }
                break;
            }

            if (aiRequestCount >= maxAiRequests && remainingCandidates.Count > 0)
            {
                requestLimitReached = true;
                foreach (var companyStatistic in companyStatistics.Values)
                {
                    companyStatistic.RequestLimitReached = true;
                }
                break;
            }
        }

        var publishedNotificationCount = await PublishStatusNotificationsAsync(
            companyStatistics.Values,
            now,
            _dateTimeProvider.Now,
            cancellationToken);

        return new CustomerInteractionAiSummaryAutomationResult(
            scannedSummaryCount,
            aiRequestCount,
            successCount,
            failedCount,
            deferredCount,
            rateLimitReached,
            requestLimitReached,
            publishedNotificationCount);
    }

    private async Task AddMonthlyCandidatesAsync(
        IDictionary<AutomationCandidateKey, AutomationCandidate> target,
        DateTime now,
        CustomerInteractionAiSummaryAutomationTarget targetPeriod,
        int scanLimit,
        CancellationToken cancellationToken)
    {
        var rows = await BaseInteractionQuery(now)
            .Where(x =>
                x.InteractionAt.Year == targetPeriod.Year &&
                x.InteractionAt.Month == targetPeriod.Month)
            .GroupBy(x => new
            {
                x.CustomerId,
                x.CompanyId,
                AuditEmployeeId = x.Customer.CreatedBy,
                CustomerCode = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                Year = x.InteractionAt.Year,
                Month = x.InteractionAt.Month
            })
            .Select(group => new
            {
                group.Key.CustomerId,
                group.Key.CompanyId,
                group.Key.AuditEmployeeId,
                group.Key.CustomerCode,
                group.Key.CustomerName,
                group.Key.Year,
                group.Key.Month,
                ActiveInteractionCount = group.Count(x => x.IsActive),
                LatestSourceChangedAt = group.Max(x => x.UpdatedDate ?? x.CreatedDate)
            })
            .ToListAsync(cancellationToken);

        var sourceCandidates = rows.Select(x => new MonthlyInteractionSource(
            x.CustomerId,
            x.CompanyId,
            x.AuditEmployeeId,
            x.CustomerCode ?? string.Empty,
            x.CustomerName ?? string.Empty,
            x.Year,
            x.Month,
            x.ActiveInteractionCount,
            x.LatestSourceChangedAt)).ToList();

        var customerIds = sourceCandidates
            .Select(x => x.CustomerId)
            .Distinct()
            .ToArray();

        var summarySnapshots = await _readDbContext.CustomerInteractionAiSummaries
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.SaleEmployeeId == null &&
                x.SummaryScope == CustomerInteractionSummaryScope.Monthly &&
                customerIds.Contains(x.CustomerId))
            .Select(x => new MonthlySummarySnapshot(
                x.CustomerId,
                x.CompanyId,
                x.Year,
                x.Month,
                x.IsAiSuccess,
                x.IsAiSkipped,
                x.SourceModel,
                x.PromptVersion,
                x.AiGeneratedDate,
                x.UpdatedDate ?? x.CreatedDate))
            .ToListAsync(cancellationToken);

        var summariesByPeriod = summarySnapshots
            .Where(x => x.Year.HasValue && x.Month.HasValue)
            .GroupBy(x => new MonthlySummaryKey(
                x.CustomerId,
                x.CompanyId,
                x.Year!.Value,
                x.Month!.Value))
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(summary => summary.LastChangedAt).First());

        var candidates = sourceCandidates
            .Where(x => NeedsMonthlySummary(
                x,
                summariesByPeriod.GetValueOrDefault(new MonthlySummaryKey(
                    x.CustomerId,
                    x.CompanyId,
                    x.Year,
                    x.Month))))
            .OrderByDescending(x => x.LatestSourceChangedAt)
            .Take(scanLimit)
            .Select(x => new AutomationCandidate(
                x.CustomerId,
                x.CompanyId,
                x.AuditEmployeeId,
                x.CustomerCode,
                x.CustomerName,
                CustomerInteractionSummaryScope.Monthly,
                x.Year,
                x.Month,
                x.LatestSourceChangedAt));

        AddCandidates(target, candidates);
    }

    private async Task AddYearlyCandidatesAsync(
        IDictionary<AutomationCandidateKey, AutomationCandidate> target,
        DateTime now,
        int targetYear,
        int scanLimit,
        CancellationToken cancellationToken)
    {
        var rows = await BaseInteractionQuery(now)
            .Where(x => x.InteractionAt.Year == targetYear)
            .GroupBy(x => new
            {
                x.CustomerId,
                x.CompanyId,
                AuditEmployeeId = x.Customer.CreatedBy,
                CustomerCode = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName,
                Year = x.InteractionAt.Year
            })
            .Select(group => new
            {
                group.Key.CustomerId,
                group.Key.CompanyId,
                group.Key.AuditEmployeeId,
                group.Key.CustomerCode,
                group.Key.CustomerName,
                group.Key.Year,
                LatestSourceChangedAt = group.Max(x => x.UpdatedDate ?? x.CreatedDate)
            })
            .OrderByDescending(x => x.LatestSourceChangedAt)
            .Take(scanLimit)
            .ToListAsync(cancellationToken);
        AddCandidates(target, rows.Select(x => new AutomationCandidate(
            x.CustomerId,
            x.CompanyId,
            x.AuditEmployeeId,
            x.CustomerCode ?? string.Empty,
            x.CustomerName ?? string.Empty,
            CustomerInteractionSummaryScope.Yearly,
            targetYear,
            null,
            x.LatestSourceChangedAt)));
    }

    private static void ValidateTarget(CustomerInteractionAiSummaryAutomationTarget? target)
    {
        if (target is null)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(target.Year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(target.Month, 12);
        ArgumentOutOfRangeException.ThrowIfLessThan(target.Month, 1);
    }

    private async Task AddLifetimeCandidatesAsync(
        IDictionary<AutomationCandidateKey, AutomationCandidate> target,
        DateTime now,
        CustomerInteractionAiSummaryAutomationTarget targetPeriod,
        int scanLimit,
        CancellationToken cancellationToken)
    {
        var targetCustomerIds = BaseInteractionQuery(now)
            .Where(x =>
                x.InteractionAt.Year == targetPeriod.Year &&
                x.InteractionAt.Month == targetPeriod.Month)
            .Select(x => x.CustomerId)
            .Distinct();

        var rows = await BaseInteractionQuery(now)
            .Where(x => targetCustomerIds.Contains(x.CustomerId))
            .GroupBy(x => new
            {
                x.CustomerId,
                x.CompanyId,
                AuditEmployeeId = x.Customer.CreatedBy,
                CustomerCode = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName
            })
            .Select(group => new
            {
                group.Key.CustomerId,
                group.Key.CompanyId,
                group.Key.AuditEmployeeId,
                group.Key.CustomerCode,
                group.Key.CustomerName,
                LatestSourceChangedAt = group.Max(x => x.UpdatedDate ?? x.CreatedDate)
            })
            .OrderByDescending(x => x.LatestSourceChangedAt)
            .Take(scanLimit)
            .ToListAsync(cancellationToken);
        AddCandidates(target, rows.Select(x => new AutomationCandidate(
            x.CustomerId,
            x.CompanyId,
            x.AuditEmployeeId,
            x.CustomerCode ?? string.Empty,
            x.CustomerName ?? string.Empty,
            CustomerInteractionSummaryScope.Lifetime,
            null,
            null,
            x.LatestSourceChangedAt)));
    }

    private async Task<int> PublishStatusNotificationsAsync(
        IEnumerable<CompanyAutomationStatistics> companyStatistics,
        DateTime startedAt,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var publishedCount = 0;

        foreach (var statistics in companyStatistics.Where(ShouldPublishStatusNotification))
        {
            await _notificationService.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = statistics.CompanyId,
                CreatedBy = statistics.AuditEmployeeId,
                CreatedByNameSnapshot = "Hệ thống AI Summary",
                Topic = TopicNotifications.CustomerAiSummaryAutomationStatus,
                Severity = ResolveNotificationSeverity(statistics),
                Title = ResolveNotificationTitle(statistics),
                Message = BuildNotificationMessage(statistics, completedAt),
                PayloadJson = BuildNotificationPayload(statistics, startedAt, completedAt),
                TargetRoles = new[] { ApplicationRoles.Developer }
            }, cancellationToken);
            publishedCount++;
        }

        return publishedCount;
    }

    private async Task PublishFailureNotificationAsync(
        AutomationCandidate candidate,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var customerDisplay = string.IsNullOrWhiteSpace(candidate.CustomerCode)
            ? candidate.CustomerName
            : $"{candidate.CustomerCode} - {candidate.CustomerName}";
        var scopeDisplay = BuildScopeDisplay(candidate);
        var safeErrorMessage = LimitText(errorMessage, 500, "AI summary không thể tạo. Vui lòng thử lại.");

        await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = candidate.CompanyId,
            CreatedBy = candidate.AuditEmployeeId,
            CreatedByNameSnapshot = "Hệ thống AI Summary",
            Topic = TopicNotifications.CustomerAiSummaryAutomationStatus,
            Severity = NotificationSeverity.Error,
            Title = $"AI Customer Summary lỗi: {customerDisplay}",
            Message = $"Khách hàng: {customerDisplay}.{Environment.NewLine}" +
                      $"Phạm vi: {scopeDisplay}.{Environment.NewLine}" +
                      $"Lý do: {safeErrorMessage}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                contentType = "CustomerAiSummaryAutomationFailure",
                customerId = candidate.CustomerId,
                customerCode = candidate.CustomerCode,
                customerName = candidate.CustomerName,
                summaryScope = candidate.SummaryScope.ToString(),
                year = candidate.Year,
                month = candidate.Month,
                errorMessage = safeErrorMessage
            }),
            TargetRoles = new[] { ApplicationRoles.Developer }
        }, cancellationToken);
    }

    private static bool ShouldPublishStatusNotification(CompanyAutomationStatistics statistics)
        => statistics.AiRequestCount > 0 || statistics.RateLimitReached;

    private static NotificationSeverity ResolveNotificationSeverity(
        CompanyAutomationStatistics statistics)
    {
        if (statistics.FailedCount > 0)
        {
            return NotificationSeverity.Error;
        }

        return statistics.RateLimitReached
            ? NotificationSeverity.Warning
            : NotificationSeverity.Info;
    }

    private static string ResolveNotificationTitle(CompanyAutomationStatistics statistics)
    {
        if (statistics.FailedCount > 0)
        {
            return "AI Customer Summary có lỗi";
        }

        return statistics.RateLimitReached
            ? "AI Customer Summary tạm dừng"
            : "AI Customer Summary đã chạy xong";
    }

    private static string BuildNotificationMessage(
        CompanyAutomationStatistics statistics,
        DateTime completedAt)
    {
        var lines = new List<string>
        {
            $"Hoàn tất lúc {completedAt:HH:mm dd/MM/yyyy}.",
            BuildScopeMessage("Tháng", statistics.GetScope(CustomerInteractionSummaryScope.Monthly)),
            BuildScopeMessage("Năm", statistics.GetScope(CustomerInteractionSummaryScope.Yearly)),
            BuildScopeMessage("Lifetime", statistics.GetScope(CustomerInteractionSummaryScope.Lifetime)),
            $"Tổng: đã xét {statistics.ScannedSummaryCount}, gọi AI {statistics.AiRequestCount}, " +
            $"thành công {statistics.SuccessCount}, lỗi {statistics.FailedCount}, " +
            $"chờ dependency {statistics.DeferredCount}, không cần gọi AI {statistics.NoAiRequestSuccessCount}."
        };

        if (statistics.RateLimitReached)
        {
            lines.Add("Gemini đã chạm giới hạn; phần còn lại sẽ tự tiếp tục ở lượt sau.");
        }
        else if (statistics.RequestLimitReached)
        {
            lines.Add("Đã đạt giới hạn request của lượt chạy; phần còn lại sẽ tự tiếp tục ở lượt sau.");
        }

        if (statistics.FailedCustomerCodes.Count > 0)
        {
            lines.Add($"Khách lỗi: {string.Join(", ", statistics.FailedCustomerCodes.Take(MaxFailedCustomerCodesInSummaryNotification))}.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildScopeMessage(
        string label,
        ScopeAutomationStatistics statistics)
        => $"{label}: đã xét {statistics.ScannedSummaryCount}, gọi AI {statistics.AiRequestCount}, " +
           $"thành công {statistics.SuccessCount}, lỗi {statistics.FailedCount}, " +
           $"chờ {statistics.DeferredCount}.";

    private static string BuildNotificationPayload(
        CompanyAutomationStatistics statistics,
        DateTime startedAt,
        DateTime completedAt)
        => JsonSerializer.Serialize(new
        {
            contentType = "CustomerAiSummaryAutomationStatus",
            startedAt,
            completedAt,
            scannedSummaryCount = statistics.ScannedSummaryCount,
            aiRequestCount = statistics.AiRequestCount,
            successCount = statistics.SuccessCount,
            failedCount = statistics.FailedCount,
            deferredCount = statistics.DeferredCount,
            noAiRequestSuccessCount = statistics.NoAiRequestSuccessCount,
            rateLimitReached = statistics.RateLimitReached,
            requestLimitReached = statistics.RequestLimitReached,
            failedCustomerCodes = statistics.FailedCustomerCodes
                .Take(MaxFailedCustomerCodesInSummaryNotification)
                .ToArray(),
            scopes = new[]
            {
                BuildScopePayload(CustomerInteractionSummaryScope.Monthly, statistics),
                BuildScopePayload(CustomerInteractionSummaryScope.Yearly, statistics),
                BuildScopePayload(CustomerInteractionSummaryScope.Lifetime, statistics)
            }
        });

    private static object BuildScopePayload(
        CustomerInteractionSummaryScope scope,
        CompanyAutomationStatistics statistics)
    {
        var scopeStatistics = statistics.GetScope(scope);
        return new
        {
            scope = scope.ToString(),
            scannedSummaryCount = scopeStatistics.ScannedSummaryCount,
            aiRequestCount = scopeStatistics.AiRequestCount,
            successCount = scopeStatistics.SuccessCount,
            failedCount = scopeStatistics.FailedCount,
            deferredCount = scopeStatistics.DeferredCount,
            noAiRequestSuccessCount = scopeStatistics.NoAiRequestSuccessCount
        };
    }

    private static CompanyAutomationStatistics GetOrCreateCompanyStatistics(
        IDictionary<Guid, CompanyAutomationStatistics> target,
        AutomationCandidate candidate)
    {
        if (target.TryGetValue(candidate.CompanyId, out var statistics))
        {
            return statistics;
        }

        statistics = new CompanyAutomationStatistics(
            candidate.CompanyId,
            candidate.AuditEmployeeId);
        target[candidate.CompanyId] = statistics;
        return statistics;
    }

    private IQueryable<HRM.Domain.Entities.CustomerSchema.CustomerInteraction> BaseInteractionQuery(DateTime now)
        => _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x =>
                x.Customer.IsActive != false &&
                x.InteractionAt >= LifetimeStart &&
                x.InteractionAt <= now);

    private static void AddCandidates(
        IDictionary<AutomationCandidateKey, AutomationCandidate> target,
        IEnumerable<AutomationCandidate> rows)
    {
        foreach (var row in rows)
        {
            target[new AutomationCandidateKey(
                row.CustomerId,
                row.SummaryScope,
                row.Year,
                row.Month)] = row;
        }
    }

    /// <summary>
    /// Chỉ đưa các kỳ tháng cần tạo/làm mới vào quota quét. Rule này khớp với cache
    /// của generation service để những summary còn mới không làm các kỳ lỗi/cũ bị đói lượt.
    /// </summary>
    private bool NeedsMonthlySummary(
        MonthlyInteractionSource source,
        MonthlySummarySnapshot? summary)
    {
        if (summary is null ||
            (!summary.IsAiSuccess && !summary.IsAiSkipped) ||
            !string.Equals(summary.SourceModel, _client.Model, StringComparison.Ordinal) ||
            summary.PromptVersion != CustomerInteractionAiSummaryGenerationService.CurrentPromptVersion)
        {
            return true;
        }

        if (source.ActiveInteractionCount == 0)
        {
            return !summary.IsAiSkipped || summary.LastChangedAt < source.LatestSourceChangedAt;
        }

        return summary.IsAiSkipped ||
               !summary.AiGeneratedDate.HasValue ||
               summary.AiGeneratedDate.Value < source.LatestSourceChangedAt;
    }

    private static GenerateCustomerInteractionAiSummaryRequest BuildRequest(
        AutomationCandidate candidate)
        => new()
        {
            SummaryScope = candidate.SummaryScope,
            Year = candidate.Year,
            Month = candidate.Month,
            ForceRegenerate = false
        };

    private static string BuildScopeDisplay(AutomationCandidate candidate)
        => candidate.SummaryScope switch
        {
            CustomerInteractionSummaryScope.Monthly => $"Tháng {candidate.Month}/{candidate.Year}",
            CustomerInteractionSummaryScope.Yearly => $"Năm {candidate.Year}",
            CustomerInteractionSummaryScope.Lifetime => "Toàn bộ lịch sử",
            _ => candidate.SummaryScope.ToString()
        };

    private static string LimitText(string? value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..maxLength]}…";
    }

    private sealed record AutomationCandidateKey(
        Guid CustomerId,
        CustomerInteractionSummaryScope SummaryScope,
        int? Year,
        int? Month);

    private sealed record AutomationCandidate(
        Guid CustomerId,
        Guid CompanyId,
        Guid AuditEmployeeId,
        string CustomerCode,
        string CustomerName,
        CustomerInteractionSummaryScope SummaryScope,
        int? Year,
        int? Month,
        DateTime LatestSourceChangedAt);

    private sealed record MonthlyInteractionSource(
        Guid CustomerId,
        Guid CompanyId,
        Guid AuditEmployeeId,
        string CustomerCode,
        string CustomerName,
        int Year,
        int Month,
        int ActiveInteractionCount,
        DateTime LatestSourceChangedAt);

    private sealed record MonthlySummaryKey(
        Guid CustomerId,
        Guid CompanyId,
        int Year,
        int Month);

    private sealed record MonthlySummarySnapshot(
        Guid CustomerId,
        Guid CompanyId,
        int? Year,
        int? Month,
        bool IsAiSuccess,
        bool IsAiSkipped,
        string? SourceModel,
        string? PromptVersion,
        DateTime? AiGeneratedDate,
        DateTime LastChangedAt);

    private sealed class CompanyAutomationStatistics
    {
        private readonly Dictionary<CustomerInteractionSummaryScope, ScopeAutomationStatistics> _scopes = new();

        public CompanyAutomationStatistics(Guid companyId, Guid auditEmployeeId)
        {
            CompanyId = companyId;
            AuditEmployeeId = auditEmployeeId;
        }

        public Guid CompanyId { get; }

        public Guid AuditEmployeeId { get; }

        public bool RateLimitReached { get; set; }

        public bool RequestLimitReached { get; set; }

        public List<string> FailedCustomerCodes { get; } = new();

        public int ScannedSummaryCount => _scopes.Values.Sum(x => x.ScannedSummaryCount);

        public int AiRequestCount => _scopes.Values.Sum(x => x.AiRequestCount);

        public int SuccessCount => _scopes.Values.Sum(x => x.SuccessCount);

        public int FailedCount => _scopes.Values.Sum(x => x.FailedCount);

        public int DeferredCount => _scopes.Values.Sum(x => x.DeferredCount);

        public int NoAiRequestSuccessCount => _scopes.Values.Sum(x => x.NoAiRequestSuccessCount);

        public ScopeAutomationStatistics GetScope(CustomerInteractionSummaryScope scope)
        {
            if (_scopes.TryGetValue(scope, out var statistics))
            {
                return statistics;
            }

            statistics = new ScopeAutomationStatistics();
            _scopes[scope] = statistics;
            return statistics;
        }

        public void Record(
            CustomerInteractionSummaryScope scope,
            CustomerInteractionAiSummaryGenerationOutcome outcome,
            bool countAiRequest = true)
        {
            var statistics = GetScope(scope);
            statistics.ScannedSummaryCount++;

            if (outcome.AiRequested)
            {
                if (countAiRequest)
                {
                    statistics.AiRequestCount++;
                }
                if (outcome.Result.Success)
                {
                    statistics.SuccessCount++;
                }
                else
                {
                    statistics.FailedCount++;
                }

                return;
            }

            if (outcome.Result.Success)
            {
                statistics.NoAiRequestSuccessCount++;
            }
            else
            {
                statistics.DeferredCount++;
            }
        }

        public void RecordFailure(AutomationCandidate candidate)
        {
            var customerDisplay = string.IsNullOrWhiteSpace(candidate.CustomerCode)
                ? candidate.CustomerName
                : candidate.CustomerCode;
            FailedCustomerCodes.Add(customerDisplay);
        }
    }

    private sealed class ScopeAutomationStatistics
    {
        public int ScannedSummaryCount { get; set; }

        public int AiRequestCount { get; set; }

        public int SuccessCount { get; set; }

        public int FailedCount { get; set; }

        public int DeferredCount { get; set; }

        public int NoAiRequestSuccessCount { get; set; }
    }
}

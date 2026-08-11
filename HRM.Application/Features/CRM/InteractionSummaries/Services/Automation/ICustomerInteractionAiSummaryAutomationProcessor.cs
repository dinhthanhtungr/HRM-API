namespace HRM.Application.Features.CRM.InteractionSummaries.Services.Automation;

/// <summary>
/// Quét các scope summary do worker lập lịch và chỉ gọi AI cho summary chưa có hoặc đã cũ.
/// </summary>
public interface ICustomerInteractionAiSummaryAutomationProcessor
{
    Task<CustomerInteractionAiSummaryAutomationResult> ProcessAsync(
        int scanCustomerLimit,
        int maxAiRequests,
        int maxCustomersPerAiRequest,
        CustomerInteractionAiSummaryAutomationRunPlan runPlan,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Kỳ tháng mà worker đang được phép tự động làm mới.
/// </summary>
public sealed record CustomerInteractionAiSummaryAutomationTarget(
    int Year,
    int Month);

/// <summary>
/// Các scope được phép chạy trong một lượt automation. Scope không có target sẽ không tạo candidate.
/// </summary>
public sealed record CustomerInteractionAiSummaryAutomationRunPlan(
    CustomerInteractionAiSummaryAutomationTarget? MonthlyTarget,
    int? YearlyTargetYear,
    CustomerInteractionAiSummaryAutomationTarget? LifetimeTarget);

public sealed record CustomerInteractionAiSummaryAutomationResult(
    int ScannedSummaryCount,
    int AiRequestCount,
    int SuccessCount,
    int FailedCount,
    int DeferredCount,
    bool RateLimitReached,
    bool RequestLimitReached,
    int PublishedNotificationCount);

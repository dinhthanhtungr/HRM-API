namespace HRM.Application.Features.PLM.SampleRequests.Dtos;

public sealed record WorkflowNotificationJobDto(string JobId, Guid EventId, Guid SampleRequestId,
    string SampleRequestCode, string EventType, string Status, DateTime CreatedAt, DateTime? ProcessedAt,
    int Attempts, int MaxAttempts, string? ErrorCode, bool CanRetry, bool CanRestoreSummary = false, bool HasMessage = false,
    bool CanReconcileWatchers = false, string? ReplayAssessment = null);

public sealed record RetryWorkflowNotificationJobResult(string Outcome);

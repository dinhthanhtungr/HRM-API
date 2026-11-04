using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>Các sự kiện nghiệp vụ bất biến; không thay thế lịch sử thay đổi dữ liệu AuditLog.</summary>
internal static class SampleRequestWorkflowEvents
{
    public const string Created = "SampleRequestWorkflowCreated";
    public const string FormulaConfirmed = "LabFormulaConfirmed";
    public const string PriceAccepted = "CustomerPriceAccepted";
    public const string PriceRevoked = "CustomerPriceAcceptanceRevoked";
    public const string StandardReceived = "LabStandardReceived";
    public const string SampleSent = "SampleSent";
    public const string FeedbackRecorded = "CustomerFeedbackRecorded";
    public const string RequestCancelled = "SampleRequestCancelled";

    public static SampleRequestWorkflowEvent Create(Guid requestId, Guid companyId, Guid employeeId,
        DateTime recordedAt, string eventType, DateTime? occurredAt = null, string? note = null,
        Guid? formulaId = null, Guid? trialId = null) => new()
    {
        SampleRequestId = requestId,
        CompanyId = companyId,
        EventType = eventType,
        OccurredAt = occurredAt ?? recordedAt,
        RecordedAt = recordedAt,
        RecordedByEmployeeId = employeeId,
        Note = note,
        FormulaId = formulaId,
        SampleRequestSampleTrialId = trialId
    };
    public static SampleRequestWorkflowEvent? CreateSharedPriceFeedback(SampleRequestWorkflowEvent primary, string? customerFeedbackNote)
    {
        if (string.IsNullOrWhiteSpace(customerFeedbackNote) || primary.RecordedByEmployeeId is not { } actor) return null;
        var shared = Create(primary.SampleRequestId, primary.CompanyId, actor, primary.RecordedAt,
            Services.Workflow.SampleFlowRules.PriceFeedbackShared, primary.OccurredAt, customerFeedbackNote.Trim(),
            primary.FormulaId, primary.SampleRequestSampleTrialId);
        shared.RelatedEventId = primary.Id;
        return shared;
    }

}

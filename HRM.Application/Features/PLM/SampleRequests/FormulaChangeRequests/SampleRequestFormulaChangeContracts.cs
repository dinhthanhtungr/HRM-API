namespace HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;

public enum SampleRequestFormulaChangeDecision
{
    Approve = 1,
    Reject = 2,
    Cancel = 3
}

public sealed class SampleRequestFormulaChangeActionDto
{
    public string Type { get; set; } = SampleRequestFormulaChangePayloadTypes.Request;
    public string Status { get; set; } = SampleRequestFormulaChangeStatuses.Pending;
    public Guid SampleRequestId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public Guid CurrentFormulaId { get; set; }
    public string CurrentFormulaExternalId { get; set; } = string.Empty;
    public Guid RequestedFormulaId { get; set; }
    public string RequestedFormulaExternalId { get; set; } = string.Empty;
    public bool CanDecide { get; set; }
    public Guid RequestedByEmployeeId { get; set; }
    public DateTime RequestedAt { get; set; }
    public Guid? DecidedByEmployeeId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}

public sealed class SampleRequestFormulaChangeDecisionResultDto
{
    public Guid ConversationId { get; set; }
    public Guid RequestMessageId { get; set; }
    public Guid ResponseMessageId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid SampleRequestId { get; set; }
    public Guid CurrentFormulaId { get; set; }
    public Guid RequestedFormulaId { get; set; }
}

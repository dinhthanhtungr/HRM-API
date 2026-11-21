namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed record SampleFlowStateDto(string WorkflowMode, string Stage, Guid? FormulaId,
    DateTime? RequestUpdatedDate, DateTime? FormulaUpdatedDate, double? RequestedKg,
    IReadOnlyList<string> AllowedActions)
{
    public string? DeliveryKind { get; init; }
    public bool ChipBeforeSample { get; init; }
    public IReadOnlyList<string> PlanActions { get; init; } = [];
    public bool DeliveryNeedsNewRequest { get; init; }
    public bool HasPendingDeliveryPlan { get; init; }
    public bool IsLabPaused { get; init; }
    public string? LabRejectionReason { get; init; }
    public IReadOnlyList<string> CompletionBlockers { get; init; } = [];
    public bool CompletionVisible { get; init; } = true;
}

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;

public sealed record SampleFlowStateDto(string WorkflowMode, string Stage, Guid? FormulaId,
    DateTime? RequestUpdatedDate, DateTime? FormulaUpdatedDate, double? RequestedKg,
    IReadOnlyList<string> AllowedActions);

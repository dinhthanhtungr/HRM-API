using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;

public sealed class ApplySampleFlowActionCommand : IRequest<OperationResult<Guid>>
{
    internal bool UseExistingTransaction { get; init; }
    public Guid SampleRequestId { get; set; }
    public string Action { get; set; } = string.Empty;
    public bool IsUrgent { get; set; }
    public DateTime? ExpectedUpdatedDate { get; set; }
    public DateTime? ExpectedFormulaUpdatedDate { get; set; }
    public double? RequestedKg { get; set; }
    public int? RequestedSets { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string? Note { get; set; }
    public string? CustomerFeedbackNote { get; set; }
    public string? PriceReference { get; set; }
    public Guid? QuotationLineId { get; set; }
    public Guid? FormulaId { get; set; }
}

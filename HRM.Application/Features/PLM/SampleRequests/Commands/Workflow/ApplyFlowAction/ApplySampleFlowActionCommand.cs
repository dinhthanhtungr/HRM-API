using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ApplyFlowAction;

public sealed class ApplySampleFlowActionCommand : IRequest<OperationResult<Guid>>
{
    public Guid SampleRequestId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime? ExpectedUpdatedDate { get; set; }
    public DateTime? ExpectedFormulaUpdatedDate { get; set; }
    public double? RequestedKg { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string? Note { get; set; }
    public string? CustomerFeedbackNote { get; set; }
    public string? PriceReference { get; set; }
    public Guid? QuotationLineId { get; set; }
}

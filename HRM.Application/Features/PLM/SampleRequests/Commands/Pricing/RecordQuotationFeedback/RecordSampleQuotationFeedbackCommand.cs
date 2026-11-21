using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordQuotationFeedback;

public sealed class RecordSampleQuotationFeedbackCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore] public Guid SampleRequestId { get; set; }
    public DateTime? QuotedAt { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
    public SampleQuotationFeedbackInput? Feedback { get; init; }
}

public sealed class SampleQuotationFeedbackInput
{
    [Required] public string Outcome { get; init; } = string.Empty;
    public DateTime? OccurredAt { get; init; }
    public Guid? QuotationLineId { get; init; }
    [StringLength(500)] public string? Reference { get; init; }
    [StringLength(2000)] public string? Note { get; init; }
    [StringLength(2000)] public string? CustomerFeedbackNote { get; init; }
}

using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
public sealed class RecordSampleOptionFeedbackCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore] internal bool UseExistingTransaction { get; set; }
    [JsonIgnore] internal bool ImplicitFeedbackWithinBatch { get; set; }
    [JsonIgnore] internal bool PreserveSiblingResults { get; set; }
    [JsonIgnore] internal bool SuppressNotification { get; set; }
    [JsonIgnore] internal bool DeferCompletion { get; set; }
    [JsonIgnore] public Guid SampleRequestId { get; set; }
    [JsonIgnore] public Guid BatchId { get; set; }
    public Guid OperationId { get; set; }
    public Guid TrialId { get; set; }
    public bool Approved { get; set; }
    public DateTime ReplyDate { get; set; }
    public string? Note { get; set; }
    public string? CorrectionReason { get; set; }
    public DateTime? ExpectedRequestUpdatedDate { get; set; }
    public DateTime? ExpectedTrialUpdatedDate { get; set; }
}

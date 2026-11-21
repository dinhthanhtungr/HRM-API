using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;

public sealed class RecordSampleBatchFeedbackCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore] public Guid SampleRequestId { get; set; }
    [JsonIgnore] public Guid BatchId { get; set; }
    public Guid OperationId { get; set; }
    public bool PreserveSiblingResults { get; set; }
    public DateTime? ExpectedRequestUpdatedDate { get; set; }
    public IReadOnlyList<RecordSampleOptionFeedbackCommand> Options { get; set; } = [];
}

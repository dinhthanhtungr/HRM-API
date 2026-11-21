using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
public sealed class SendSampleOptionsCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore] public Guid SampleRequestId { get; set; }
    public Guid BatchId { get; set; }
    public DateTime? ExpectedRequestUpdatedDate { get; set; }
    public DateTime SentDate { get; set; }
    public IReadOnlyList<SendSampleOption> Options { get; set; } = [];
}

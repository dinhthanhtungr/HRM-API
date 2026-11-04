using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleRequestSampleTrialCustomerFeedback;

/// <summary>
/// Records Sale's customer feedback for one delivered sample trial.
/// Terminal feedback also advances the related SampleRequest and Formula lifecycle.
/// </summary>
public sealed class RecordSampleRequestSampleTrialCustomerFeedbackCommand
    : IRequest<OperationResult<Guid>>
{
    [JsonIgnore]
    public Guid SampleRequestId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SampleTrialStatus Status { get; init; }

    public string? CustomerReplyStatus { get; init; }
    public DateTime? CustomerReplyDate { get; init; }
    public string? CustomerReplyNote { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordCustomerFeedbackNote;

public sealed class RecordCustomerFeedbackNoteCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore] public Guid SampleRequestId { get; set; }
    public DateTime? ExpectedRequestUpdatedDate { get; set; }
    public Guid ExpectedPriceEventId { get; set; }
    [Required, StringLength(2000)] public string? CustomerFeedbackNote { get; set; }
}

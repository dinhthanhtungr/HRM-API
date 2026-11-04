using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleWorkSchedule;

/// <summary>Only saved planning dates; omitted or null dates are unchanged, clearFields explicitly removes a date.</summary>
public sealed class UpdateSampleWorkScheduleCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore]
    public Guid SampleRequestId { get; set; }
    public DateTime? ExpectedUpdatedDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? ExpectedPriceQuoteDate { get; set; }
    public string[]? ClearFields { get; set; }
}

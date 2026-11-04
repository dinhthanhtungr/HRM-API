using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmCustomerPrice;

public sealed class ConfirmCustomerPriceCommand : IRequest<OperationResult<Guid>>
{
    [JsonIgnore] public Guid SampleRequestId { get; set; }
    public bool Revoke { get; init; }
    public DateTime? CustomerAcceptedAt { get; init; }
    public Guid? QuotationLineId { get; init; }
    [StringLength(500)] public string? Reference { get; init; }
    [StringLength(2000)] public string? CustomerFeedbackNote { get; init; }
    [StringLength(2000)] public string? Note { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
}

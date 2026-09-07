using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabelDetail;

public sealed class CreateCustomerLabelDetailCommand : IRequest<OperationResult<SaveCustomerLabelDetailResultDto>>
{
    [JsonIgnore]
    public Guid CustomerLabelHeaderId { get; set; }
    public int LineNo { get; init; }
    public string FieldKey { get; init; } = string.Empty;
    public string? FieldValue { get; init; }
    public bool IsActive { get; init; } = true;
}

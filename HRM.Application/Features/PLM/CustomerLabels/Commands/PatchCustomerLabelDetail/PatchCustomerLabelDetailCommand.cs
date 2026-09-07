using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.PatchCustomerLabelDetail;

/// <summary>
/// PATCH một detail. FieldValue chỉ được xóa khi ClearFieldValue là true.
/// </summary>
public sealed class PatchCustomerLabelDetailCommand : IRequest<OperationResult<SaveCustomerLabelDetailResultDto>>
{
    [JsonIgnore]
    public Guid CustomerLabelHeaderId { get; set; }
    [JsonIgnore]
    public Guid CustomerLabelDetailId { get; set; }
    public int? LineNo { get; init; }
    public string? FieldKey { get; init; }
    public string? FieldValue { get; init; }
    public bool? IsActive { get; init; }
    public bool ClearFieldValue { get; init; }
}

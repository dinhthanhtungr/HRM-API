using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using MediatR;
using System.Text.Json.Serialization;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.PatchCustomerLabel;

/// <summary>
/// PATCH header: field không gửi giữ nguyên; chuỗi nullable chỉ được xóa qua ClearFields.
/// </summary>
public sealed class PatchCustomerLabelCommand : IRequest<OperationResult<SaveCustomerLabelResultDto>>
{
    [JsonIgnore]
    public Guid CustomerLabelHeaderId { get; set; }
    public string? ColorCode { get; init; }
    public string? CustomerExternalId { get; init; }
    public string? LabelType { get; init; }
    public bool? IsActive { get; init; }
    public IReadOnlyList<string>? ClearFields { get; init; }
}

using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.CustomerLabels.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabel;

/// <summary>
/// Tạo mẫu nhãn khách hàng và các dòng detail ban đầu. Product và Customer phải thuộc công ty hiện tại.
/// </summary>
public sealed class CreateCustomerLabelCommand : IRequest<OperationResult<SaveCustomerLabelResultDto>>
{
    public Guid ProductId { get; init; }
    public string? ColorCode { get; init; }
    public Guid CustomerId { get; init; }
    public string? CustomerExternalId { get; init; }
    public string? LabelType { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<CreateCustomerLabelDetailRequest> Details { get; init; } = Array.Empty<CreateCustomerLabelDetailRequest>();
}

public sealed class CreateCustomerLabelDetailRequest
{
    public int LineNo { get; init; }
    public string FieldKey { get; init; } = string.Empty;
    public string? FieldValue { get; init; }
    public bool IsActive { get; init; } = true;
}

using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummary;

/// <summary>
/// Yêu cầu tạo hoặc tái sử dụng AI summary cho lịch sử tương tác của một khách hàng.
/// </summary>
public sealed class GenerateCustomerInteractionSummaryCommand
    : IRequest<OperationResult<CustomerInteractionAiSummaryDto>>
{
    public Guid CustomerId { get; init; }
    public GenerateCustomerInteractionAiSummaryRequest Request { get; init; } = new();
}

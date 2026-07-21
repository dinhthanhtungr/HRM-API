using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummaryBatch;

/// <summary>
/// Yêu cầu tạo AI summary cho nhiều khách hàng trong một batch có giới hạn.
/// </summary>
public sealed class GenerateCustomerInteractionSummaryBatchCommand
    : IRequest<OperationResult<CustomerInteractionAiSummaryBatchDto>>
{
    public GenerateCustomerInteractionAiSummaryBatchRequest Request { get; init; } = new();
}

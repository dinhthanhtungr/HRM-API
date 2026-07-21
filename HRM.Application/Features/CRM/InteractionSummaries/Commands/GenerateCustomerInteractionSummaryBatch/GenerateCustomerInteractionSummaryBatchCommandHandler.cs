using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummary;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummaryBatch;

/// <summary>
/// Điều phối tối đa 100 lệnh tạo summary đơn, giữ kết quả từng khách hàng và dừng khi hết quota AI.
/// </summary>
internal sealed class GenerateCustomerInteractionSummaryBatchCommandHandler
    : IRequestHandler<GenerateCustomerInteractionSummaryBatchCommand, OperationResult<CustomerInteractionAiSummaryBatchDto>>
{
    private const int MaxCustomers = 100;
    private readonly ISender _sender;

    public GenerateCustomerInteractionSummaryBatchCommandHandler(ISender sender) => _sender = sender;

    /// <summary>
    /// Xử lý lần lượt các customer id duy nhất để mỗi item vẫn dùng đầy đủ validation và visibility check của lệnh đơn.
    /// </summary>
    public async Task<OperationResult<CustomerInteractionAiSummaryBatchDto>> Handle(
        GenerateCustomerInteractionSummaryBatchCommand command,
        CancellationToken cancellationToken)
    {
        var ids = command.Request.CustomerIds.Where(x => x != Guid.Empty).Distinct().Take(MaxCustomers + 1).ToArray();
        if (ids.Length == 0 || ids.Length > MaxCustomers)
            return OperationResult<CustomerInteractionAiSummaryBatchDto>.Fail($"Batch must contain between 1 and {MaxCustomers} customers.");
        var dto = new CustomerInteractionAiSummaryBatchDto { TotalRequested = ids.Length };
        foreach (var id in ids)
        {
            var result = await _sender.Send(new GenerateCustomerInteractionSummaryCommand
            {
                CustomerId = id,
                Request = new GenerateCustomerInteractionAiSummaryRequest
                {
                    SummaryScope = command.Request.SummaryScope, SaleEmployeeId = command.Request.SaleEmployeeId,
                    Year = command.Request.Year, Month = command.Request.Month, From = command.Request.From,
                    To = command.Request.To, ForceRegenerate = command.Request.ForceRegenerate
                }
            }, cancellationToken);
            dto.Items.Add(new CustomerInteractionAiSummaryBatchItemDto
            {
                CustomerId = id, Success = result.Success, Message = result.Message, Summary = result.Data
            });
            if (result.Success) dto.SuccessCount++; else dto.FailedCount++;
            dto.RateLimit = result.Data?.RateLimit ?? dto.RateLimit;
            if (dto.RateLimit is { CanRequest: false }) break;
        }
        return OperationResult<CustomerInteractionAiSummaryBatchDto>.Ok(dto);
    }
}

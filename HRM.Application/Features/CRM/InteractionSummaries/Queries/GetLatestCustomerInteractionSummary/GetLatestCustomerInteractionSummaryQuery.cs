using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Features.CRM.InteractionSummaries.Queries.GetLatestCustomerInteractionSummary;

/// <summary>
/// Truy vấn bản AI summary mới nhất của một khách hàng mà người dùng được phép xem.
/// </summary>
public sealed record GetLatestCustomerInteractionSummaryQuery(
    Guid CustomerId,
    CustomerInteractionSummaryScope? SummaryScope = null,
    int? Year = null,
    int? Month = null)
    : IRequest<OperationResult<CustomerInteractionAiSummaryDto>>;

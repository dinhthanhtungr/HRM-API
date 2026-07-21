using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerInteractionsByCustomer;

/// <summary>
/// Lấy lịch sử interaction của một customer theo bộ lọc, chỉ trả dữ liệu trong customer visibility scope.
/// </summary>
public sealed class GetCustomerInteractionsByCustomerQuery
    : IRequest<OperationResult<PagedResult<CustomerInteractionDto>>>
{
    public Guid CustomerId { get; init; }
    public CustomerInteractionQuery Query { get; init; } = new();
}

using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerPurchaseHealth;

/// <summary>
/// Lấy danh sách khách theo lịch sử mua hàng để ưu tiên chăm lại khách đã lâu không phát sinh giao hàng.
/// </summary>
public sealed class GetCustomerPurchaseHealthQuery
    : IRequest<OperationResult<CustomerPurchaseHealthReportDto>>
{
    public CustomerPurchaseHealthQuery Query { get; init; } = new();
}

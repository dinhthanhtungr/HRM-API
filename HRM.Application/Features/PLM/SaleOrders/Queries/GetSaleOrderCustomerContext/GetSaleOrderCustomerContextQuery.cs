using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrderCustomerContext;

/// <summary>
/// Lấy dữ liệu mặc định cho header tạo SaleOrder sau khi chọn khách hàng.
/// </summary>
public sealed record GetSaleOrderCustomerContextQuery(Guid CustomerId)
    : IRequest<SaleOrderCustomerContextDto?>;

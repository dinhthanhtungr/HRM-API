using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetLastSaleOrderByCustomer;

/// <summary>
/// Lấy thông tin bán gần nhất và giá gợi ý hiện hành của một sản phẩm cho khách hàng trong công ty hiện tại.
/// </summary>
public sealed record GetLastSaleOrderByCustomerQuery(
    Guid CustomerId,
    Guid ProductId) : IRequest<SaleOrderProductDefaultsDto?>;

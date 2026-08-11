using HRM.Application.Features.PLM.SaleOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrderById;

/// <summary>
/// Lấy chi tiết một SaleOrder active theo id trong phạm vi công ty hiện tại.
/// </summary>
public sealed record GetSaleOrderByIdQuery(Guid MerchandiseOrderId) : IRequest<SaleOrderDetailDto?>;

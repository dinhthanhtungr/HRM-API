using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
public sealed record GetPurchaseOrderByIdQuery(Guid PurchaseOrderId) : IRequest<PurchaseOrderDetailDto?>;

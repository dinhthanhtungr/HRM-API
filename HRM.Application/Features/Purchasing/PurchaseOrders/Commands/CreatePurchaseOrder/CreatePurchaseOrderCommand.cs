using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
public sealed record CreatePurchaseOrderCommand(CreatePurchaseOrderRequest Request) : IRequest<OperationResult<PurchaseOrderDetailDto>>;

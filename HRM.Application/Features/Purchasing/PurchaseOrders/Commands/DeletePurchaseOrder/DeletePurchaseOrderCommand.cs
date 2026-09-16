using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.DeletePurchaseOrder;

public sealed record DeletePurchaseOrderCommand(Guid PurchaseOrderId) : IRequest<OperationResult>;

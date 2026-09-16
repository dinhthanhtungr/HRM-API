using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CompletePurchaseOrder;
public sealed record CompletePurchaseOrderCommand(Guid PurchaseOrderId) : IRequest<OperationResult>;

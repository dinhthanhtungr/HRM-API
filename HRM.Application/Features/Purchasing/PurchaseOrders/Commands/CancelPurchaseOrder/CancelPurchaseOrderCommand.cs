using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CancelPurchaseOrder;
public sealed record CancelPurchaseOrderCommand(Guid PurchaseOrderId, string? Reason) : IRequest<OperationResult>;

using HRM.Application.Commons.Models;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.SubmitPurchaseOrder;
public sealed record SubmitPurchaseOrderCommand(Guid PurchaseOrderId) : IRequest<OperationResult>;

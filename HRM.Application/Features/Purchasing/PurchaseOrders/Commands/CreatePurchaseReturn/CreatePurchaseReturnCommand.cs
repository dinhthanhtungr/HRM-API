using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseReturn;
public sealed record CreatePurchaseReturnCommand(Guid PurchaseOrderId, CreatePurchaseReturnRequest Request)
    : IRequest<OperationResult<PurchaseReturnRequestDto>>;

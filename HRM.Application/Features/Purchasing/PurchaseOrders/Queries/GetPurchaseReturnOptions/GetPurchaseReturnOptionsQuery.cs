using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseReturnOptions;
public sealed record GetPurchaseReturnOptionsQuery(Guid PurchaseOrderId, bool IncludeAccepted = false) : IRequest<OperationResult<PurchaseReturnOptionsDto>>;

using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderReceiptProgress;

public sealed record GetPurchaseOrderReceiptProgressQuery(Guid PurchaseOrderId, bool IncludeInactive = false)
    : IRequest<PurchaseOrderReceiptProgressDto?>;

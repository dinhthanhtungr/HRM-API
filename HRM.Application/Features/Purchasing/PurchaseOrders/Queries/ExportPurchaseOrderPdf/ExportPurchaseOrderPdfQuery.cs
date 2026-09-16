using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderPdf;

public sealed record ExportPurchaseOrderPdfQuery(Guid PurchaseOrderId)
    : IRequest<OperationResult<PurchaseOrderFileDto>>;

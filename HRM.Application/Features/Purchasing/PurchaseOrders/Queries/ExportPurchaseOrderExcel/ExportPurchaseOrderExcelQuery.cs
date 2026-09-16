using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderExcel;

public sealed record ExportPurchaseOrderExcelQuery(Guid PurchaseOrderId)
    : IRequest<OperationResult<PurchaseOrderFileDto>>;

using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrdersExcel;

public sealed record ExportPurchaseOrdersExcelQuery(GetPurchaseOrdersQuery Filter) : IRequest<PurchaseOrderFileDto>;

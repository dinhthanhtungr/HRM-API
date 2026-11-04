using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrdersExcel;

internal sealed class ExportPurchaseOrdersExcelQueryHandler(PurchaseOrderListExportService service)
    : IRequestHandler<ExportPurchaseOrdersExcelQuery, PurchaseOrderFileDto>
{
    public async Task<PurchaseOrderFileDto> Handle(ExportPurchaseOrdersExcelQuery query, CancellationToken ct) => new()
    {
        Content = await service.ExportAsync(query.Filter, ct),
        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        FileName = $"PurchaseOrders_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
    };
}

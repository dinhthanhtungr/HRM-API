using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderExcel;

internal sealed class ExportPurchaseOrderExcelQueryHandler(
    ISender sender,
    IPurchaseOrderExcelRenderer renderer,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<ExportPurchaseOrderExcelQuery, OperationResult<PurchaseOrderFileDto>>
{
    public async Task<OperationResult<PurchaseOrderFileDto>> Handle(
        ExportPurchaseOrderExcelQuery request,
        CancellationToken cancellationToken)
    {
        if (!PurchaseOrderAccess.CanManage(permissions))
            return OperationResult<PurchaseOrderFileDto>.Fail("Bạn không có quyền xuất PO.");

        var purchaseOrder = await sender.Send(new GetPurchaseOrderByIdQuery(request.PurchaseOrderId), cancellationToken);
        if (purchaseOrder is null)
            return OperationResult<PurchaseOrderFileDto>.Fail("Không tìm thấy đơn mua hàng.");

        return OperationResult<PurchaseOrderFileDto>.Ok(new PurchaseOrderFileDto
        {
            FileName = $"PO-{Sanitize(purchaseOrder.ExternalId)}.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Content = renderer.Render(purchaseOrder)
        });
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
}

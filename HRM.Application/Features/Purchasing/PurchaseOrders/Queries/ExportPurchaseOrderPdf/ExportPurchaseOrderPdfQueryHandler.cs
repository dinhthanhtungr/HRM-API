using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderPdf;

internal sealed class ExportPurchaseOrderPdfQueryHandler(
    ISender sender,
    IPurchaseOrderPdfRenderer renderer,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<ExportPurchaseOrderPdfQuery, OperationResult<PurchaseOrderFileDto>>
{
    public async Task<OperationResult<PurchaseOrderFileDto>> Handle(
        ExportPurchaseOrderPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (!PurchaseOrderAccess.CanManage(permissions))
            return OperationResult<PurchaseOrderFileDto>.Fail("Bạn không có quyền xuất PO.");

        var purchaseOrder = await sender.Send(new GetPurchaseOrderByIdQuery(request.PurchaseOrderId), cancellationToken);
        if (purchaseOrder is null)
            return OperationResult<PurchaseOrderFileDto>.Fail("Không tìm thấy đơn mua hàng.");

        return OperationResult<PurchaseOrderFileDto>.Ok(new PurchaseOrderFileDto
        {
            FileName = $"PO-{Sanitize(purchaseOrder.ExternalId)}.pdf",
            ContentType = "application/pdf",
            Content = renderer.Render(purchaseOrder)
        });
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
}

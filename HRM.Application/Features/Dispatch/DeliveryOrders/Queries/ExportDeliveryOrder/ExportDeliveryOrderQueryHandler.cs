using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryOrder;

internal sealed class ExportDeliveryOrderQueryHandler(
    IDispatchReadDbContext db,
    ICurrentUser currentUser,
    IDeliveryOrderPdfRenderer pdf,
    IDeliveryOrderExcelRenderer excel) : IRequestHandler<ExportDeliveryOrderQuery, DeliveryOrderFileDto?>
{
    public async Task<DeliveryOrderFileDto?> Handle(ExportDeliveryOrderQuery request, CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanRead(currentUser) || request.Id == Guid.Empty ||
            currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return null;

        var document = await DeliveryOrderDocumentQuery.Project(db.DeliveryOrders.AsNoTracking(), request.Id, companyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null) return null;

        cancellationToken.ThrowIfCancellationRequested();
        // Tên file chỉ dùng GUID server-owned, không lấy tên/mã có ký tự tùy ý từ dữ liệu.
        return request.Excel
            ? new($"delivery-order-{document.Id:D}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.Render(document))
            : new($"delivery-order-{document.Id:D}.pdf", "application/pdf", pdf.Render(document));
    }
}

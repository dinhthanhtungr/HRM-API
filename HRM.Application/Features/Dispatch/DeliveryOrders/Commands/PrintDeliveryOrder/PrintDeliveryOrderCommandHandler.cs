using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.PrintDeliveryOrder;

internal sealed class PrintDeliveryOrderCommandHandler(
    IDispatchWriteDbContext db, IDispatchReadDbContext read, ICurrentUser user, IDeliveryOrderPdfRenderer renderer,
    DeliveryDocumentFormulaService formulaDisplay)
    : IRequestHandler<PrintDeliveryOrderCommand, OperationResult<DeliveryOrderFileDto>>
{
    public async Task<OperationResult<DeliveryOrderFileDto>> Handle(PrintDeliveryOrderCommand request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanManage(user) || request.Id == Guid.Empty ||
            user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<DeliveryOrderFileDto>.Fail("Bạn không có quyền hoặc thiếu thông tin nhân viên/công ty.");

        await using var transaction = await db.BeginDeliveryOrderTransactionAsync(ct);
        var claimed = await db.DeliveryOrders.Where(x => x.Id == request.Id && x.CompanyId == companyId &&
                x.IsActive && x.Status != "Canceled" && x.Status != "Cancelled" && !x.HasPrinted)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.HasPrinted, true), ct);
        var order = await db.DeliveryOrders.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.Id && x.CompanyId == companyId && x.IsActive, ct);
        if (order is null || order.Status is "Canceled" or "Cancelled")
            return OperationResult<DeliveryOrderFileDto>.Fail("Không tìm thấy phiếu giao hợp lệ để in.");

        var document = await DeliveryOrderDocumentQuery.Project(read.DeliveryOrders.AsNoTracking(), request.Id, companyId)
            .SingleAsync(ct);
        await formulaDisplay.EnrichAsync(document, companyId, ct);
        // Render trong transaction: lỗi dựng PDF rollback HasPrinted; lượng giao đã được ghi nhận khi lưu.
        var file = new DeliveryOrderFileDto($"delivery-order-{request.Id:D}.pdf", "application/pdf", renderer.Render(document));
        if (claimed == 0)
        {
            // UPDATE chờ khóa record; kiểm tra lại để không trả thành công nếu vừa bị hủy.
            if (!await db.DeliveryOrders.AnyAsync(x => x.Id == request.Id && x.CompanyId == companyId &&
                x.IsActive && x.HasPrinted && x.Status != "Canceled" && x.Status != "Cancelled", ct))
                return OperationResult<DeliveryOrderFileDto>.Fail("Phiếu giao vừa bị hủy hoặc vô hiệu hóa.");
            await transaction.CommitAsync(ct);
            return OperationResult<DeliveryOrderFileDto>.Ok(file);
        }

        await transaction.CommitAsync(ct);
        return OperationResult<DeliveryOrderFileDto>.Ok(file);
    }
}

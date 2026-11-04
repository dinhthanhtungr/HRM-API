using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.RollbackProductionOrderStock;

/// <summary>Stocked chưa có VA/lịch/chứng từ kho → New; giữ kế hoạch, không hoàn tác kho hay đơn bán.</summary>
internal sealed class RollbackProductionOrderStockCommandHandler(
    IManufacturingDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    IDateTimeProvider clock, IEventLogWriter timeline)
    : IRequestHandler<RollbackProductionOrderStockCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RollbackProductionOrderStockCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageProductionOrders, true);
        if (error is not null) return OperationResult.Fail(error);
        if (!ManufacturingAccess.CanManageInform(user, permissions))
            return OperationResult.Fail("Bạn không có quyền quay lệnh dùng tồn kho về Mới.");
        if (command.Id == Guid.Empty || command.Revision?.Length != 64)
            return OperationResult.Fail("Thiếu revision của lệnh. Vui lòng tải lại Inform.");
        var company = user.CompanyId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(command.Id, company, ct);
            var link = await db.MfgOrderPOs.Include(x => x.ProductionOrder)
                .FirstOrDefaultAsync(x => x.IsActive && x.MfgProductionOrderId == command.Id
                    && x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == company
                    && x.Detail.IsActive && x.Detail.MerchandiseOrder.IsActive
                    && x.Detail.MerchandiseOrder.CompanyId == company, ct);
            if (link is null) return OperationResult.Fail("Không tìm thấy lệnh liên kết đơn hàng trong công ty.");
            var order = link.ProductionOrder;
            if (!await ManufacturingEditorRevision.MatchesAsync(db, order, command.Revision, ct))
                return OperationResult.Fail(ManufacturingEditorRevision.Conflict);
            if (order.Status != ManufacturingProductOrder.Stocked.ToString())
                return OperationResult.Fail("Chỉ lệnh dùng tồn kho (Stocked) mới được quay về Mới.");
            if (await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company
                    && x.MfgProductionOrderId == command.Id, ct)
                || await db.SchedualMfgs.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == command.Id, ct)
                || await db.OperationMaterialRequestLinks.AsNoTracking().AnyAsync(x => x.CompanyId == company
                    && x.MfgProductionOrderId == command.Id, ct)
                || await db.ProductionOutputReceiptSources.AsNoTracking().AnyAsync(x => x.CompanyId == company
                    && x.MfgProductionOrderId == command.Id, ct))
                return OperationResult.Fail("Lệnh đã có VA, lịch hoặc dữ liệu kho liên quan; không thể quay về Mới tự động.");
            order.Status = ManufacturingProductOrder.New.ToString();
            order.UpdatedDate = clock.Now;
            order.UpdatedBy = user.EmployeeId!.Value;
            await timeline.AddAsync(new EventLogCreateRequest
            {
                CompanyId = company, EmployeeId = user.EmployeeId.Value,
                SourceId = order.MfgProductionOrderId, SourceCode = order.ExternalId,
                SourceType = "MfgProductionOrder", EventType = EventType.ManufacturingProductOrder,
                Status = order.Status, CreatedDate = clock.Now,
                Note = "Hủy lựa chọn dùng tồn kho, quay về Mới để chuẩn bị công thức sản xuất."
            }, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult.Ok("Đã quay về Mới. Bạn có thể chọn công thức và tạo VA.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult.Fail("Không thể quay lệnh về Mới. Vui lòng tải lại dữ liệu.");
        }
    }
}

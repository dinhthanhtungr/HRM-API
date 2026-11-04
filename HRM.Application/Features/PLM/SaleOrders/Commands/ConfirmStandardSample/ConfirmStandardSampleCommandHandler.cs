using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.ConfirmStandardSample;

/// <summary>Sale phụ trách hoặc nhóm được cấp quyền xác nhận từng dòng; không duyệt đơn hoặc tạo thêm MFG.</summary>
internal sealed class ConfirmStandardSampleCommandHandler(ISaleOrderDbContext db, ICurrentUser user,
    IDateTimeProvider clock, IEventLogWriter events) : IRequestHandler<ConfirmStandardSampleCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ConfirmStandardSampleCommand request, CancellationToken ct)
    {
        var company = SaleOrderCurrentUserGuard.RequireCompanyId(user);
        var actor = SaleOrderCurrentUserGuard.RequireEmployeeId(user);
        var canConfirmOtherOwners = StandardSampleConfirmationRules.CanConfirmOtherOwners(user);
        await using var transaction = await db.BeginTransactionAsync(ct);
        // Acquire the parent row lock before loading details, also shared by manual approval.
        await db.MerchandiseOrders.Where(x => x.MerchandiseOrderId == request.OrderId && x.CompanyId == company
                && x.IsActive && (x.ManagerById == actor || canConfirmOtherOwners))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedDate, x => x.UpdatedDate), ct);
        var order = await db.MerchandiseOrders.Include(x => x.MerchandiseOrderDetails)
            .FirstOrDefaultAsync(x => x.MerchandiseOrderId == request.OrderId && x.CompanyId == company
                && x.IsActive && (x.ManagerById == actor || canConfirmOtherOwners), ct);
        if (order is null || !StandardSampleConfirmationRules.CanConfirm(user, company, order.ManagerById))
            return OperationResult.Fail("Bạn không có quyền xác nhận mẫu chuẩn của đơn hàng này.");
        if (order.Status is nameof(MerchadiseStatus.Cancelled) or nameof(MerchadiseStatus.Completed) or nameof(MerchadiseStatus.Delivered))
            return OperationResult.Fail("Đơn hàng đã kết thúc hoặc bị hủy.");
        var detail = order.MerchandiseOrderDetails.FirstOrDefault(x => x.MerchandiseOrderDetailId == request.DetailId && x.IsActive);
        if (detail is null) return OperationResult.Fail("Không tìm thấy dòng đơn hàng.");
        if (detail.Status != nameof(MerchadiseStatus.WaitingForStandardSample))
            return OperationResult.Ok("Dòng đơn hàng không còn chờ mẫu chuẩn.");

        var now = clock.Now;
        var mfgs = await db.MfgOrderPOs.Where(x => x.IsActive && x.MerchandiseOrderDetailId == request.DetailId
                && x.ProductionOrder.CompanyId == company && x.ProductionOrder.IsActive)
            .Select(x => x.ProductionOrder).ToListAsync(ct);
        foreach (var mfg in mfgs.Where(x => x.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample)))
        {
            mfg.Status = nameof(ManufacturingProductOrder.New);
            mfg.UpdatedBy = actor;
            mfg.UpdatedDate = now;
            await events.AddAsync(new EventLogCreateRequest
            {
                CompanyId = company, EmployeeId = actor, SourceType = "MfgProductionOrder",
                SourceId = mfg.MfgProductionOrderId, SourceCode = mfg.ExternalId,
                ParentSourceType = nameof(MerchandiseOrder), ParentSourceId = order.MerchandiseOrderId,
                EventType = EventType.ManufacturingProductOrder, Status = mfg.Status,
                Note = "Sale xác nhận mẫu chuẩn; mở thao tác kế hoạch.", CreatedDate = now
            }, ct);
        }
        detail.Status = nameof(MerchadiseStatus.New);
        order.UpdatedBy = actor;
        order.UpdatedDate = now;
        await events.AddAsync(new EventLogCreateRequest
        {
            CompanyId = company, EmployeeId = actor, SourceType = nameof(MerchandiseOrderDetail),
            SourceId = detail.MerchandiseOrderDetailId, SourceCode = order.ExternalId,
            ParentSourceType = nameof(MerchandiseOrder), ParentSourceId = order.MerchandiseOrderId,
            EventType = EventType.MerchadiseStatus, Status = detail.Status, CreatedDate = now,
            Note = "Sale xác nhận đã có mẫu chuẩn.",
            PayloadJson = JsonSerializer.Serialize(new { detail.ProductId, detail.FormulaId })
        }, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult.Ok("Đã xác nhận mẫu chuẩn.");
    }
}

using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.CustomerCare.Visibility;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.FinishDelivery;

internal sealed class FinishDeliveryCommandHandler(IDispatchWriteDbContext db, ICurrentUser user, IEventLogWriter timeline,
    ICustomerVisibilityService visibility, ICustomerVisibilityReadDbContext customerDb)
    : IRequestHandler<FinishDeliveryCommand, OperationResult>
{
    public async Task<OperationResult> Handle(FinishDeliveryCommand request, CancellationToken ct)
    {
        if (!DeliveryOrderAccessRules.CanFinish(user) || user.CompanyId is not { } companyId || companyId == Guid.Empty ||
            user.EmployeeId is not { } employeeId || employeeId == Guid.Empty || request.MerchandiseOrderId == Guid.Empty)
            return OperationResult.Fail("Bạn không có quyền hoặc thiếu thông tin PO/công ty/nhân viên.");
        await using var transaction = await db.BeginDeliveryOrderTransactionAsync(ct);
        var now = DateTime.Now;
        var orders = db.MerchandiseOrders.AsQueryable();
        if (!DeliveryOrderAccessRules.CanManage(user))
        {
            var scope = await visibility.BuildScopeAsync(ct);
            if (scope.CompanyId != companyId || scope.EmployeeId != employeeId)
                return OperationResult.Fail("Phạm vi khách hàng không hợp lệ.");
            orders = visibility.ApplyMerchandiseOrderVisibility(orders, customerDb.Customers.AsNoTracking(), scope);
            orders = orders.Where(x => !x.IsDeliveryPaused || x.DeliveryPauseType == null ||
                x.DeliveryPauseType.Trim().ToLower() != "paymenthold");
        }
        var changed = await orders.Where(x => x.MerchandiseOrderId == request.MerchandiseOrderId &&
                x.CompanyId == companyId && x.IsActive &&
                (x.Status == null ||
                 (x.Status != nameof(MerchadiseStatus.Cancelled) &&
                  x.Status != nameof(MerchadiseStatus.Completed) &&
                  x.Status != nameof(MerchadiseStatus.Delivered))))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, nameof(MerchadiseStatus.Delivered))
                .SetProperty(x => x.UpdatedBy, employeeId).SetProperty(x => x.UpdatedDate, now), ct);
        if (changed == 0)
            return OperationResult.Fail("PO không tồn tại, đã bị khóa hoặc đã hủy/giao xong/hoàn thành.");
        var code = await db.MerchandiseOrders.Where(x => x.MerchandiseOrderId == request.MerchandiseOrderId && x.CompanyId == companyId)
            .Select(x => x.ExternalId).SingleAsync(ct);
        await timeline.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId, CompanyId = companyId, SourceType = "MerchandiseOrder",
            SourceId = request.MerchandiseOrderId, SourceCode = code, EventType = EventType.MerchadiseStatus,
            Status = nameof(MerchadiseStatus.Delivered), Note = "Xác nhận giao xong.", CreatedDate = now
        }, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult.Ok("Xác nhận giao hàng thành công.");
    }
}

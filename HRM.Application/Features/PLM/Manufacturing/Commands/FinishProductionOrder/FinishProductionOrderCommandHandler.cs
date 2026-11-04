using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.FinishProductionOrder;

internal sealed class FinishProductionOrderCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IEventLogWriter timeline
) : IRequestHandler<FinishProductionOrderCommand, OperationResult>
{
    // Legacy checks this exact note written by the March repair job, rather than any Finished event.
    private const string LegacyAutomationNote = "Ket thuc lenh san xuat automation 25/03/2026";
    public async Task<OperationResult> Handle(FinishProductionOrderCommand request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageProductionOrders, true);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(request.Id, company, ct);
            var link = await db.MfgOrderPOs.Where(x => x.IsActive && x.MfgProductionOrderId == request.Id && x.ProductionOrder.CompanyId == company
                && x.ProductionOrder.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company)
                .Include(x => x.ProductionOrder)
                .FirstOrDefaultAsync(ct);
            if (link is null)
            {
                return OperationResult.Fail("Không tìm thấy lệnh sản xuất liên kết đơn hàng trong công ty.");
            }

            var order = link.ProductionOrder;
            if (!ManufacturingAccess.IsInformOperator(user))
                return OperationResult.Fail("Chỉ PLPUUser, President và Developer được kết thúc lệnh.");
            if (order.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample))
                return OperationResult.Fail("Lệnh đang chờ Sale xác nhận mẫu chuẩn.");
            var finished = ManufacturingProductOrder.Finished.ToString();
            if (string.Equals(order.Status, finished, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Ok("Lệnh sản xuất đã ở trạng thái Finished.");
            }

            var now = clock.Now;
            order.Status = finished;
            order.UpdatedDate = now;
            order.UpdatedBy = employee;
            var schedule = await db.SchedualMfgs.FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.Id, ct);
            if (schedule is not null)
            {
                schedule.Status = finished;
            }

            var existedLog = await db.EventLogs.AsNoTracking().AnyAsync(e => e.CompanyId == company && e.SourceId == request.Id
                && e.EventType == EventType.ManufacturingProductOrder
                && e.Status == finished
                && e.Note == LegacyAutomationNote, ct);
            if (!existedLog)
            {
                await timeline.AddAsync(new EventLogCreateRequest
                {
                    EmployeeId = employee,
                    CompanyId = company,
                    SourceType = "MfgProductionOrder",
                    SourceId = request.Id,
                    SourceCode = order.ExternalId,
                    EventType = EventType.ManufacturingProductOrder,
                    Status = finished,
                    CreatedDate = now,
                    Note = $"Ket thuc lenh san xuat automation vào {now:dd/MM/yyyy HH:mm:ss} boi {user.UserName}"
                }, ct);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult.Ok("Kết thúc lệnh sản xuất thành công.");
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult.Fail("Có lỗi xảy ra trong quá trình kết thúc lệnh sản xuất.");
        }
    }
}

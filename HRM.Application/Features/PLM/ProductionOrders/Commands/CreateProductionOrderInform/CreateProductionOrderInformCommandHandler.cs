using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;

internal sealed class CreateProductionOrderInformCommandHandler(
    IProductionOrderDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions,
    IExternalIdService externalIds, IDateTimeProvider clock, IEventLogWriter timeline,
    ProductionOrderReferenceValidator references, ProductionOrderReservationService reservations)
    : IRequestHandler<CreateProductionOrderInformCommand, OperationResult<CreateProductionOrderInformResult>>
{
    public async Task<OperationResult<CreateProductionOrderInformResult>> Handle(
        CreateProductionOrderInformCommand command, CancellationToken ct)
    {
        var error = ProductionOrderCreationAccess.Validate(user, permissions) ??
                    ProductionOrderCreationRules.ValidateInform(command.Request);
        if (error is not null) return OperationResult<CreateProductionOrderInformResult>.Fail(error);
        var request = command.Request;
        var companyId = user.CompanyId!.Value;
        var employeeId = user.EmployeeId!.Value;
        var items = ProductionOrderCreationRules.ActiveItems(request);

        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            error = await references.ValidateInformAsync(request, companyId, ct);
            if (error is not null) return OperationResult<CreateProductionOrderInformResult>.Fail(error);
            var now = clock.Now;
            var order = ProductionOrderInformFactory.Create(request, companyId, employeeId, now,
                await externalIds.GenerateMonthlyCodeAsync(companyId, DocumentPrefix.MFG.ToString(), ct));
            await db.MfgProductionOrders.AddAsync(order, ct);
            await db.MfgOrderPOs.AddAsync(new MfgOrderPO
            {
                MfgProductionOrderId = order.MfgProductionOrderId,
                MerchandiseOrderDetailId = request.MerchandiseOrderDetailId, IsActive = true
            }, ct);

            ManufacturingFormula? formula = null;
            if (items.Count > 0)
            {
                formula = ProductionOrderFormulaFactory.Create(request, companyId, employeeId, now,
                    await externalIds.GenerateMonthlyCodeAsync(companyId, DocumentPrefix.VA.ToString(), ct));
                await db.ManufacturingFormulas.AddAsync(formula, ct);
                await db.ProductionSelectVersions.AddAsync(ProductionOrderFormulaFactory.CreateSelection(order, formula, now), ct);
                await db.SchedualMfgs.AddAsync(ProductionOrderFormulaFactory.CreateSchedule(order, now), ct);
                if ((order.TotalQuantity ?? 0m) > 0m)
                {
                    var reserveResult = await reservations.SyncAsync(order, items, now, ct);
                    if (!reserveResult.Success)
                        return OperationResult<CreateProductionOrderInformResult>.Fail(reserveResult.Message!);
                }
            }

            await timeline.AddAsync(new EventLogCreateRequest
            {
                EmployeeId = employeeId, CompanyId = companyId, SourceType = "MfgProductionOrder",
                SourceId = order.MfgProductionOrderId, SourceCode = order.ExternalId,
                ParentSourceType = "MerchandiseOrder", ParentSourceId = request.MerchandiseOrderId,
                EventType = EventType.ManufacturingProductOrder, Status = order.Status,
                Note = items.Count > 0
                    ? $"Tạo lệnh sản xuất kèm công thức bởi {user.UserName} vào {now}"
                    : $"Tạo lệnh sản xuất bởi {user.UserName} vào {now}", CreatedDate = now
            }, ct);
            // Compatibility: legacy tìm link đã persist trước SaveChanges, không dùng link đang Added.
            // Không tự sửa thứ tự này vì sẽ làm CreateInform chuyển SaleOrder sang Processing khác code cũ.
            var persistedOrderId = await db.MfgOrderPOs.AsNoTracking()
                .Where(x => x.MfgProductionOrderId == order.MfgProductionOrderId && x.IsActive &&
                            x.Detail.MerchandiseOrder.CompanyId == companyId)
                .Select(x => (Guid?)x.Detail.MerchandiseOrderId).FirstOrDefaultAsync(ct);
            if (persistedOrderId.HasValue)
                await SyncSaleOrderAsync(persistedOrderId.Value, companyId, employeeId, now, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<CreateProductionOrderInformResult>.Ok(
                new(order.MfgProductionOrderId, order.ExternalId, formula?.ManufacturingFormulaId, formula?.ExternalId),
                formula is null ? "Tạo lệnh sản xuất thành công." : "Tạo lệnh sản xuất và công thức thành công.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult<CreateProductionOrderInformResult>.Fail("Có lỗi xảy ra trong quá trình tạo lệnh sản xuất.");
        }
    }

    private async Task SyncSaleOrderAsync(Guid orderId, Guid companyId, Guid employeeId, DateTime now, CancellationToken ct)
    {
        var saleOrder = await db.MerchandiseOrders.FirstOrDefaultAsync(
            x => x.MerchandiseOrderId == orderId && x.CompanyId == companyId && x.IsActive, ct);
        if (saleOrder is null) return;
        if (!Enum.TryParse<MerchadiseStatus>(saleOrder.Status, true, out var status)) status = MerchadiseStatus.New;
        if (status is not (MerchadiseStatus.Approved or MerchadiseStatus.New)) return;
        saleOrder.Status = MerchadiseStatus.Processing.ToString();
        saleOrder.UpdatedDate = now;
        saleOrder.UpdatedBy = employeeId;
        await timeline.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId, CompanyId = companyId, SourceType = "MerchandiseOrder",
            SourceId = saleOrder.MerchandiseOrderId, SourceCode = saleOrder.ExternalId,
            EventType = EventType.MerchadiseStatus, Status = saleOrder.Status,
            Note = $"Cập nhật bởi hệ thống vào {now} bởi {user.UserName}", CreatedDate = now
        }, ct);
    }
}

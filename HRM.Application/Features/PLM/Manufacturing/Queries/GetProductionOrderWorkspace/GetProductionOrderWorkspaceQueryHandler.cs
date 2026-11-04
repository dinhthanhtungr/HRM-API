using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderWorkspace;

internal sealed class GetProductionOrderWorkspaceQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    ISender sender
) : IRequestHandler<GetProductionOrderWorkspaceQuery, OperationResult<ProductionOrderWorkspaceDto>>
{
    public async Task<OperationResult<ProductionOrderWorkspaceDto>> Handle(GetProductionOrderWorkspaceQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null)
        {
            return OperationResult<ProductionOrderWorkspaceDto>.Fail(error);
        }

        var company = user.CompanyId!.Value;
        var detail = await sender.Send(new GetProductionOrderQuery(request.Id), ct);
        if (!detail.Success || detail.Data is null)
        {
            return OperationResult<ProductionOrderWorkspaceDto>.Fail(detail.Message ?? "Không tìm thấy lệnh sản xuất.");
        }

        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.Id && x.CompanyId == company && x.IsActive, ct);
        if (order is null)
        {
            return OperationResult<ProductionOrderWorkspaceDto>.Fail("Không tìm thấy lệnh sản xuất.");
        }

        var options = await ManufacturingFormulaOptionReader.ReadAsync(db, order, ct);
        var hasLink = await db.MfgOrderPOs.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == request.Id && x.IsActive && x.ProductionOrder.CompanyId == company
            && x.Detail.IsActive
            && x.Detail.MerchandiseOrder.IsActive
            && x.Detail.MerchandiseOrder.CompanyId == company, ct);
        var canManageNew = user.EmployeeId is { } employee && employee != Guid.Empty && hasLink
            && permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
            && order.Status == ManufacturingProductOrder.New.ToString();
        var canCreateFormula = user.EmployeeId is { } formulaEmployee && formulaEmployee != Guid.Empty && hasLink
            && (permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
                || permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas))
            && order.Status == ManufacturingProductOrder.New.ToString();
        var batches = db.ManufacturingFormulaAdjustmentBatches.AsNoTracking().Where(x => x.IsActive && x.ManufacturingFormulaAdjustment.IsActive
            && x.ManufacturingFormulaAdjustment.CompanyId == company
            && x.ManufacturingFormulaAdjustment.MfgProductionOrderId == request.Id);
        var additional = await batches.Where(x => x.IsAdditionalBatch).Select(x => x.BatchNo).Distinct().CountAsync(ct);
        var rows = await batches.SelectMany(b => b.Items.Select(i => new { b.BatchNo, b.TrialNo, b.IsAdditionalBatch, b.AdjustmentType, b.BatchQuantity, i.MaterialId, i.ProductId, i.BaseQuantity, i.AdjustedQuantity, i.DeltaQuantity, i.Unit }))
            .ToListAsync(ct);
        var delta = MfgAdjustmentQuantityCalculator.CalculateItemTotals(rows.Select(x => new MfgAdjustmentQuantityRow(
            x.BatchNo,
            x.TrialNo,
            x.IsAdditionalBatch,
            x.AdjustmentType,
            x.BatchQuantity,
            x.MaterialId.HasValue ? $"M:{x.MaterialId:N}" : $"P:{x.ProductId:N}",
            x.BaseQuantity,
            x.AdjustedQuantity,
            x.DeltaQuantity,
            x.Unit
        )))
            .Sum(x => x.DeltaFromOriginalGram) / 1000m;
        return OperationResult<ProductionOrderWorkspaceDto>.Ok(new ProductionOrderWorkspaceDto(detail.Data, options, canManageNew, canCreateFormula && options.Count > 0, additional, delta)
        {
            ProductLots = await ManufacturingProductStockReader.ReadAsync(db, company, order.ProductId, ct),
            AllowedInformationActions = user.EmployeeId is { } actor && actor != Guid.Empty && hasLink
                && permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders) ? Enum.GetValues<ManufacturingInformationAction>().Where(x => ManufacturingInformationActionRules.CanApply(order.Status, x))
                .Select(x => x.ToString())
                .ToArray() : []
        });
    }
}

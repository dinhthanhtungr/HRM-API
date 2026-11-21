using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.PLM.MaterialProcessing.Queries.GetMaterialProcessings;
internal sealed class GetMaterialProcessingsQueryHandler(IMaterialProcessingDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetMaterialProcessingsQuery, MaterialProcessingListDto?>
{
    public async Task<MaterialProcessingListDto?> Handle(GetMaterialProcessingsQuery request, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewMaterialProcessing) || user.CompanyId is null) return null;
        var page = Math.Clamp(request.PageNumber, 1, 100000);
        var query = SemiFinishedProcessing.Orders(db, user.CompanyId.Value).AsNoTracking();
        var count = await query.CountAsync(ct);
        var orders = await query.Include(x => x.Product).OrderByDescending(x => x.CreatedDate).ThenBy(x => x.MfgProductionOrderId)
            .Skip((page - 1) * 20).Take(20)
            .ToListAsync(ct);
        var ids = orders.Select(x => x.MfgProductionOrderId).ToArray();
        var selections = await db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && ids.Contains(x.MfgProductionOrderId)
                && x.ValidTo == null && x.ManufacturingFormulaId != null)
            .OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ProductionSelectVersionId).ToListAsync(ct);
        var formulaIds = selections.Select(x => x.ManufacturingFormulaId!.Value).Distinct().ToArray();
        var formulas = await db.ManufacturingFormulas.AsNoTracking().Include(x => x.ManufacturingFormulaMaterials).ThenInclude(x => x.Material)
            .Where(x => x.CompanyId == user.CompanyId && x.IsActive && formulaIds.Contains(x.ManufacturingFormulaId)).ToDictionaryAsync(x => x.ManufacturingFormulaId, ct);
        var rows = new List<MaterialProcessingSummaryDto>();
        foreach (var order in orders)
        {
            var selected = selections.FirstOrDefault(x => x.MfgProductionOrderId == order.MfgProductionOrderId);
            if (selected?.ManufacturingFormulaId is not { } formulaId || !formulas.TryGetValue(formulaId, out var formula)) continue;
            var x = SemiFinishedProcessing.Adapt(order, formula);
            rows.Add(new(x.MaterialProcessingFormulaId, x.ExternalId, x.ProcessingType, x.MaterialId, x.Material.ExternalId ?? "", x.Material.Name ?? "",
                x.TotalQuantityRequest, x.NumOfBatches, x.StepOfProduct, x.ManufacturingDate, x.Details.Count, x.Status, x.Revision, x.CreatedDate, x.UpdatedDate));
        }
        return new(rows, count, page);
    }
}

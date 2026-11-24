using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Deliveries;
using HRM.Domain.Enums.Deliveries;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Services;

public sealed record ComplaintOrderVaOption(Guid ManufacturingFormulaId, string ExternalId);

internal static class ComplaintOrderSourceReader
{
    internal static async Task<List<ComplaintOrderVaOption>> ReadVaAsync(
        ISaleOrderDbContext db, Guid companyId, Guid detailId, CancellationToken ct)
    {
        var producedIds = await db.MfgOrderPOs.AsNoTracking()
            .Where(x => x.IsActive && x.MerchandiseOrderDetailId == detailId
                && x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == companyId)
            .SelectMany(x => x.ProductionOrder.ProductionSelectVersions)
            .Where(x => x.CompanyId == companyId && x.ValidFrom != null && x.ManufacturingFormulaId != null)
            .Select(x => x.ManufacturingFormulaId!.Value)
            .Distinct().ToListAsync(ct);
        var deliveries = await db.DeliveryOrderDetails.AsNoTracking()
            .Where(x => x.IsActive && !x.IsAttach && x.MerchandiseOrderDetailId == detailId
                && x.DeliveryOrder.IsActive && x.DeliveryOrder.CompanyId == companyId
                && x.DeliveryOrder.Status == nameof(DeliveryOrderStatus.Completed))
            .Select(x => new { x.LotNoList, Lots = x.LotConsumptions.Where(y => y.IsActive).Select(y => y.LotNo).ToList() })
            .ToListAsync(ct);
        var deliveredCodes = deliveries.SelectMany(x => x.Lots.Count > 0
                ? x.Lots.Select(code => code.Trim())
                : DeliveryOrderLotReadRules.SplitLegacy(x.LotNoList))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return await db.ManufacturingFormulas.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive
                && (producedIds.Contains(x.ManufacturingFormulaId) || deliveredCodes.Contains(x.ExternalId)))
            .OrderBy(x => x.ExternalId)
            .Select(x => new ComplaintOrderVaOption(x.ManufacturingFormulaId, x.ExternalId))
            .ToListAsync(ct);
    }
}

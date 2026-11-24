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
        => (await ReadVaByDetailAsync(db, companyId, [detailId], ct))[detailId];

    internal static async Task<Dictionary<Guid, List<ComplaintOrderVaOption>>> ReadVaByDetailAsync(
        ISaleOrderDbContext db, Guid companyId, Guid[] detailIds, CancellationToken ct)
    {
        if (detailIds.Length == 0) return [];

        var produced = await db.MfgOrderPOs.AsNoTracking()
            .Where(x => x.IsActive && detailIds.Contains(x.MerchandiseOrderDetailId)
                && x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == companyId)
            .SelectMany(x => x.ProductionOrder.ProductionSelectVersions
                .Where(version => version.CompanyId == companyId && version.ValidFrom != null && version.ManufacturingFormulaId != null),
                (link, version) => new { link.MerchandiseOrderDetailId, Id = version.ManufacturingFormulaId!.Value })
            .Distinct().ToListAsync(ct);
        var deliveries = await db.DeliveryOrderDetails.AsNoTracking()
            .Where(x => x.IsActive && !x.IsAttach && x.MerchandiseOrderDetailId.HasValue
                && detailIds.Contains(x.MerchandiseOrderDetailId.Value)
                && x.DeliveryOrder.IsActive && x.DeliveryOrder.CompanyId == companyId
                && x.DeliveryOrder.Status != nameof(DeliveryOrderStatus.Canceled)
                && x.DeliveryOrder.Status != "Cancelled")
            .Select(x => new { DetailId = x.MerchandiseOrderDetailId!.Value, x.LotNoList,
                Lots = x.LotConsumptions.Where(y => y.IsActive).Select(y => y.LotNo).ToList() })
            .ToListAsync(ct);
        var delivered = deliveries.SelectMany(x => (x.Lots.Count > 0
                ? x.Lots.Select(code => code.Trim())
                : DeliveryOrderLotReadRules.SplitLegacy(x.LotNoList))
            .Select(code => (x.DetailId, Code: code))).ToList();
        var producedIds = produced.Select(x => x.Id).Distinct().ToArray();
        var deliveredCodes = delivered.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var formulas = await db.ManufacturingFormulas.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive
                && (producedIds.Contains(x.ManufacturingFormulaId) || deliveredCodes.Contains(x.ExternalId)))
            .OrderBy(x => x.ExternalId)
            .Select(x => new ComplaintOrderVaOption(x.ManufacturingFormulaId, x.ExternalId))
            .ToListAsync(ct);
        var byId = formulas.ToDictionary(x => x.ManufacturingFormulaId);
        var byCode = formulas.ToDictionary(x => x.ExternalId, StringComparer.OrdinalIgnoreCase);
        var producedByDetail = produced.ToLookup(x => x.MerchandiseOrderDetailId, x => x.Id);
        var deliveredByDetail = delivered.ToLookup(x => x.DetailId, x => x.Code);
        return detailIds.ToDictionary(id => id, id => producedByDetail[id].Select(key => byId.GetValueOrDefault(key))
            .Concat(deliveredByDetail[id].Select(code => byCode.GetValueOrDefault(code)))
            .OfType<ComplaintOrderVaOption>()
            .DistinctBy(x => x.ManufacturingFormulaId)
            .OrderBy(x => x.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }
}

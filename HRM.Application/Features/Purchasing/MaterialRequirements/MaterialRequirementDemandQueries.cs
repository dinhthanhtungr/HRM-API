using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Purchasing.MaterialRequirements;

internal static class MaterialRequirementDemandQueries
{
    public static IQueryable<MfgProductionOrder> EligibleOrders(
        IQueryable<MfgProductionOrder> orders, Guid companyId, DateTime? fromDate, DateTime? toDate)
    {
        var query = orders.Where(x => x.CompanyId == companyId && x.IsActive &&
            (x.Status == nameof(ManufacturingProductOrder.Scheduled) ||
             x.Status == nameof(ManufacturingProductOrder.Scheduling) ||
             x.Status == nameof(ManufacturingProductOrder.Waiting)));
        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.RequiredDate >= from);
        }
        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(x => x.RequiredDate.Date <= to);
        }
        return query;
    }

    public static IQueryable<MaterialReservationDemand> OpenDemand(IQueryable<WarehouseTempStock> reservations)
        => reservations.Where(x => x.ReserveStatus == nameof(ReserveStatus.Open))
            .GroupBy(x => new { Code = x.Code.Trim().ToUpper(), VaCode = x.VaCode.Trim().ToUpper() })
            .Select(g => new MaterialReservationDemand
            {
                Code = g.Key.Code,
                VaCode = g.Key.VaCode,
                RequestedQuantityKg = g.Sum(x => x.QtyRequest ?? 0m),
                UsedQuantityKg = g.Sum(x => x.QtyUsed ?? 0m),
                Quantity = g.Sum(x => (x.QtyRequest ?? 0m) > (x.QtyUsed ?? 0m)
                    ? (x.QtyRequest ?? 0m) - (x.QtyUsed ?? 0m) : 0m)
            }).Where(x => x.Quantity > 0);
}

internal sealed class MaterialReservationDemand
{
    public string Code { get; init; } = string.Empty;
    public string VaCode { get; init; } = string.Empty;
    public decimal RequestedQuantityKg { get; init; }
    public decimal UsedQuantityKg { get; init; }
    public decimal Quantity { get; init; }
}

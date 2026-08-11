using HRM.Application.Commons.Deliveries;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.BackfillLotConsumptions;

internal static class DeliveryOrderLotBackfillReconciliationRules
{
    public static DeliveryOrderLotBackfillReconciliation Build(
        IReadOnlyCollection<DeliveryOrderLotBackfillSource> sourceRows,
        IReadOnlyDictionary<Guid, decimal> consumptionQuantityByDetail)
    {
        var legacyRows = sourceRows
            .Where(x => DeliveryOrderLotReadRules.SplitLegacy(x.LotNoList).Count > 0)
            .ToArray();
        var convertedRows = legacyRows
            .Where(x => consumptionQuantityByDetail.ContainsKey(x.DeliveryOrderDetailId))
            .ToArray();
        var legacyQuantityTotal = legacyRows.Sum(x => x.Quantity);
        var consumptionQuantityTotal = legacyRows.Sum(x =>
            consumptionQuantityByDetail.GetValueOrDefault(x.DeliveryOrderDetailId));

        return new DeliveryOrderLotBackfillReconciliation(
            legacyRows.Length,
            convertedRows.Length,
            convertedRows.Count(x =>
                consumptionQuantityByDetail[x.DeliveryOrderDetailId] != x.Quantity),
            legacyQuantityTotal,
            consumptionQuantityTotal,
            consumptionQuantityTotal - legacyQuantityTotal);
    }
}

internal sealed record DeliveryOrderLotBackfillSource(
    Guid DeliveryOrderDetailId,
    string? LotNoList,
    decimal Quantity);

internal sealed record DeliveryOrderLotBackfillReconciliation(
    int LegacyRowCount,
    int ConvertedRowCount,
    int QuantityMismatchRowCount,
    decimal LegacyQuantityTotal,
    decimal ConsumptionQuantityTotal,
    decimal QuantityDifference);

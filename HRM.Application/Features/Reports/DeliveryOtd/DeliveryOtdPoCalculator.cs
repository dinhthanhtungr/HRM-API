namespace HRM.Application.Features.Reports.DeliveryOtd;

internal static class DeliveryOtdPoCalculator
{
    internal static DeliveryOtdPoSummary Summarize(IEnumerable<DeliveryOtdPoDates> source, DateTime asOf)
    {
        var orders = source.ToList();
        var evaluated = orders.Where(x => x.HasDeliveryPauseHistory || x.PromisedDate is null || x.PromisedDate.Value.Date <= asOf.Date).ToList();
        var onTime = evaluated.Count(x => x.HasDeliveryPauseHistory || x.PromisedDate is null
            || (x.LatestDeliveryDate.HasValue && x.LatestDeliveryDate.Value.Date <= x.PromisedDate.Value.Date));
        var late = evaluated.Count(x => !x.HasDeliveryPauseHistory && x.PromisedDate.HasValue && x.LatestDeliveryDate.HasValue
            && x.LatestDeliveryDate.Value.Date > x.PromisedDate.Value.Date);
        return new(orders.Count, evaluated.Count, onTime, late, evaluated.Count - onTime - late,
            evaluated.Count == 0 ? null : Math.Round((decimal)onTime / evaluated.Count * 100, 2));
    }
}

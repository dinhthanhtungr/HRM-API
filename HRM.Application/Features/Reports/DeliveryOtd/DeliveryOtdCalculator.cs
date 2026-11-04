namespace HRM.Application.Features.Reports.DeliveryOtd;

internal sealed record DeliveryOtdReceipt(DateTime Date, decimal Quantity);

internal static class DeliveryOtdCalculator
{
    internal static DeliveryOtdDetailDto Evaluate(DeliveryOtdDetailDto line,
        IEnumerable<DeliveryOtdReceipt> receipts, DateTime asOf)
    {
        var events = receipts.Where(x => x.Quantity > 0 && x.Date.Date <= asOf.Date)
            .OrderBy(x => x.Date).ToList();
        decimal delivered = 0, onTime = 0;
        DateTime? completed = null;
        foreach (var receipt in events)
        {
            delivered += receipt.Quantity;
            if (line.DueDate.HasValue && receipt.Date.Date <= line.DueDate.Value.Date)
                onTime += receipt.Quantity;
            if (completed is null && delivered >= line.OrderedQuantity)
                completed = receipt.Date.Date;
        }
        var remaining = Math.Max(0, line.OrderedQuantity - delivered);
        var due = line.DueDate?.Date;
        var status = due is null ? (completed.HasValue ? DeliveryOtdStatus.OnTime : DeliveryOtdStatus.MissingDueDate)
            : completed.HasValue ? (completed.Value <= due.Value ? DeliveryOtdStatus.OnTime : DeliveryOtdStatus.Late)
            : due.Value < asOf.Date ? DeliveryOtdStatus.OverdueIncomplete : DeliveryOtdStatus.NotDue;
        return line with
        {
            DeliveredQuantity = delivered,
            OnTimeQuantity = due is null && completed.HasValue ? line.OrderedQuantity : Math.Min(line.OrderedQuantity, onTime),
            RemainingQuantity = remaining, CompletedDate = completed, Status = status,
            LateDays = completed.HasValue && due.HasValue ? Math.Max(0, (completed.Value - due.Value).Days) : null,
            OverdueDays = remaining > 0 && due.HasValue ? Math.Max(0, (asOf.Date - due.Value).Days) : 0
        };
    }

    internal static DeliveryOtdSummary Summarize(IEnumerable<DeliveryOtdDetailDto> source, DateTime asOf)
    {
        var rows = source.ToList();
        var due = rows.Where(x => (x.DueDate.HasValue && x.DueDate.Value.Date <= asOf.Date)
            || (x.DueDate is null && x.Status == DeliveryOtdStatus.OnTime)).ToList();
        var late = due.Where(x => x.Status == DeliveryOtdStatus.Late).ToList();
        var onTime = due.Count(x => x.Status == DeliveryOtdStatus.OnTime);
        var quantity = due.Sum(x => x.OrderedQuantity);
        return new(rows.Count, due.Count, onTime, late.Count,
            due.Count(x => x.Status == DeliveryOtdStatus.OverdueIncomplete),
            rows.Count(x => x.DueDate.HasValue && x.DueDate.Value.Date > asOf.Date),
            rows.Count(x => x.Status == DeliveryOtdStatus.MissingDueDate), quantity, due.Sum(x => x.OnTimeQuantity),
            due.Where(x => x.OverdueDays > 0).Sum(x => x.RemainingQuantity),
            Percent(onTime, due.Count), Percent(due.Sum(x => x.OnTimeQuantity), quantity),
            late.Count == 0 ? null : Math.Round(late.Average(x => (decimal)x.LateDays!.Value), 2));
    }

    private static decimal? Percent(decimal value, decimal total)
        => total == 0 ? null : Math.Round(value / total * 100, 2);

    internal static DeliveryOtdChartsDto Charts(IReadOnlyList<DeliveryOtdDetailDto> rows, DateTime asOf)
    {
        List<DeliveryOtdChartPoint> Group(IEnumerable<DeliveryOtdDetailDto> items,
            Func<DeliveryOtdDetailDto, string> key, Func<DeliveryOtdDetailDto, string> label)
            => items.GroupBy(key).OrderBy(x => x.Key).Select(g =>
                new DeliveryOtdChartPoint(g.Key, label(g.First()), Summarize(g, asOf))).ToList();
        return new(
            Group(rows.Where(x => x.DueDate.HasValue || x.Status == DeliveryOtdStatus.OnTime),
                x => (x.DueDate ?? x.RequestedDate).ToString("yyyy-MM"),
                x => (x.DueDate ?? x.RequestedDate).ToString("yyyy-MM")),
            Group(rows, x => x.CustomerId.ToString(), x => x.CustomerName),
            Group(rows, x => x.ProductId.ToString(), x => x.ProductName),
            Group(rows, x => x.Status.ToString(), x => x.Status.ToString()),
            Group(rows.Where(x => x.Status == DeliveryOtdStatus.OverdueIncomplete), AgingKey, AgingKey));
    }

    private static string AgingKey(DeliveryOtdDetailDto x)
        => x.OverdueDays <= 3 ? "01-03" : x.OverdueDays <= 7 ? "04-07" : x.OverdueDays <= 14 ? "08-14" : "15+";
}

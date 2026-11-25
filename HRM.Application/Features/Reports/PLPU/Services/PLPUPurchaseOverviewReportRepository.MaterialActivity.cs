using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportRepository
{
    public async Task<PLPUMaterialActivityDto?> GetMaterialActivityAsync(
        PLPUPurchaseOverviewQuery query, CancellationToken ct)
    {
        if (query.CompanyId is null || query.CompanyId == Guid.Empty)
            throw new InvalidOperationException("A current company is required for PLPU reports.");

        if (query.From is not { } start || query.To is not { } end)
            return null;

        var from = start.Date;
        var to = end.Date;
        var current = await ReadActivityAsync(query, ct);
        ActivityRows? prior = null;
        if (from.Year > 1)
        {
            var previousQuery = Queries.GetPLPUBody.PLPUMonthChartWindow.CreateQuery(
                query, from.AddYears(-1), to.AddYears(-1));
            prior = await ReadActivityAsync(previousQuery, ct);
        }

        PLPUMaterialActivityRow BuildRow(DateTime rowFrom, DateTime rowTo)
        {
            PLPUMaterialActivityCounts? previous = null;
            if (prior is not null)
            {
                var previousFrom = rowFrom.AddYears(-1);
                var previousTo = rowTo == to
                    ? to.AddYears(-1)
                    : new DateTime(previousFrom.Year, previousFrom.Month,
                        DateTime.DaysInMonth(previousFrom.Year, previousFrom.Month));
                previous = CountActivity(prior, previousFrom, previousTo);
            }
            return new(rowFrom, rowTo, CountActivity(current, rowFrom, rowTo), previous);
        }

        var months = new List<PLPUMaterialActivityRow>();
        for (var first = from; first <= to;)
        {
            var monthEnd = new DateTime(first.Year, first.Month, DateTime.DaysInMonth(first.Year, first.Month));
            var last = monthEnd < to ? monthEnd : to;
            months.Add(BuildRow(first, last));
            if (last == to) break;
            first = last.AddDays(1);
        }

        return new(BuildRow(from, to), months);
    }

    private async Task<ActivityRows> ReadActivityAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct)
    {
        var receipts = await BuildReceiptQuery(query, byMovementDate: true)
            .Select(x => new ActivityReceipt(x.ProductCode, x.ReceiptDate, x.QtyKg))
            .ToListAsync(ct);
        var returns = await BuildReceiptQuery(query, byMovementDate: true, supplierReturns: true)
            .Select(x => new ActivityReturn(x.WarehouseRequestId, x.ProductCode, x.LotNumber, x.ReceiptDate, x.QtyKg))
            .ToListAsync(ct);
        var special = await BuildReceiptQuery(query, byMovementDate: true, specialDecisions: true)
            .Select(x => new ActivityDecision(x.ProductCode, x.QCCreatedDate))
            .Distinct()
            .ToListAsync(ct);
        return new(receipts, returns, special);
    }

    private static PLPUMaterialActivityCounts CountActivity(ActivityRows rows, DateTime from, DateTime to)
    {
        var until = to.AddDays(1);
        bool InRange(DateTime date) => date >= from && date < until;
        var receipts = rows.Receipts.Where(x => InRange(x.ReceiptDate)).ToList();
        var returns = rows.Returns.Where(x => InRange(x.Date))
            .GroupBy(x => (x.RequestId, Code: x.Code.Trim().ToUpperInvariant(), Lot: x.Lot.Trim().ToUpperInvariant()))
            .Where(x => x.Sum(row => row.DeltaKg) < 0)
            .Select(x => x.Key.Code);
        var special = rows.Special
            .Where(x => x.Date is { } date && InRange(date))
            .Select(x => x.Code);

        return new(CountCodes(receipts.Select(x => x.Code)), receipts.Sum(x => x.Kg),
            CountCodes(returns), CountCodes(special));
    }

    private static int CountCodes(IEnumerable<string> codes) => codes
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Select(code => code.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    private sealed record ActivityReceipt(string Code, DateTime ReceiptDate, decimal Kg);
    private sealed record ActivityDecision(string Code, DateTime? Date);
    private sealed record ActivityReturn(int? RequestId, string Code, string Lot, DateTime Date, decimal DeltaKg);
    private sealed record ActivityRows(List<ActivityReceipt> Receipts, List<ActivityReturn> Returns, List<ActivityDecision> Special);
}

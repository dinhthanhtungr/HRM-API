namespace HRM.Application.Features.Reports.CompanyOverview;

public static class CompanyPurchaseCalculator
{
    public static IReadOnlyList<CompanyMonthlyPurchasesDto> BuildMonthly(DateTime from, DateTime to,
        IReadOnlyList<PLPU.PLPUPurchaseReportChartPointDto> raw, IReadOnlyList<CompanyPurchaseFact> facts)
    {
        var result = new List<CompanyMonthlyPurchasesDto>();
        for (var month = new DateTime(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var until = month.AddMonths(1) < to.Date.AddDays(1) ? month.AddMonths(1) : to.Date.AddDays(1);
            var orders = facts.Where(x => x.Kind == "Supplies" && x.Source == "PurchaseOrder"
                && x.Date >= month && x.Date < until).ToArray();
            result.Add(new(month, raw.Where(x => x.PeriodStart == month).Sum(x => x.ReceivedPurchaseValue),
                orders.Sum(x => x.Amount ?? 0m), orders.Count(x => !x.Amount.HasValue)));
        }
        return result;
    }

    public static IReadOnlyList<CompanyPurchaseFact> ReplaceSupplyOrders(
        IReadOnlyList<CompanyPurchaseFact> existingFacts, IReadOnlyList<CompanyPurchaseFact> viewOrders)
    {
        var replacedKeys = viewOrders.Select(x => (x.PurchaseOrderId, Code: x.ItemCode.ToUpperInvariant())).ToHashSet();
        // Keep receipts and other purchase groups unchanged. A reclassified historical
        // item must not remain in Unclassified as well as Supplies.
        return existingFacts.Where(x => x.Source != "PurchaseOrder"
                || (x.Kind != "Supplies" && !replacedKeys.Contains((x.PurchaseOrderId, x.ItemCode.ToUpperInvariant()))))
            .Concat(viewOrders).ToArray();
    }

    public static CompanyPurchasesDto Build(IReadOnlyList<CompanyPurchaseFact> facts)
    {
        var groups = new[] { "Supplies", "Services", "Other", "Unclassified" }
            .Select(kind =>
            {
                var rows = facts.Where(x => x.Kind == kind).ToArray();
                var orders = rows.Where(x => x.Source == "PurchaseOrder").ToArray();
                var receipts = rows.Where(x => x.Source != "PurchaseOrder").ToArray();
                return new CompanyPurchaseGroupDto
                {
                    Kind = kind,
                    OrderCount = orders.Select(x => x.PurchaseOrderId).Distinct().Count(),
                    KnownOrderedAmount = orders.Sum(x => x.Amount ?? 0m),
                    OrderedAmountIssueCount = orders.Count(x => !x.Amount.HasValue),
                    ReceiptCount = receipts.Select(x => (x.Source, x.DocumentKey)).Distinct().Count(),
                    ReceiptLineCount = receipts.Length,
                    KnownReceivedAmount = receipts.Sum(x => x.Amount ?? 0m),
                    ReceivedAmountIssueCount = receipts.Count(x => !x.Amount.HasValue),
                    ReceivedQuantities = receipts.Where(x => x.IncludeQuantity)
                        .GroupBy(x => x.Unit.Trim(), StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x.Key)
                        .Select(x => new CompanyPurchaseQuantityDto(x.Key, x.Sum(r => r.Quantity))).ToArray()
                };
            }).ToArray();
        var issues = facts.Where(x => x.Issue != null)
            .OrderByDescending(x => x.Date).ThenBy(x => x.Source).ThenBy(x => x.DocumentKey).ThenBy(x => x.ItemCode)
            .ToArray();
        return new CompanyPurchasesDto
        {
            Groups = groups,
            IssueCount = issues.Length,
            Issues = issues.Take(100).Select(x => new CompanyPurchaseIssueDto(
                x.Source, x.DocumentCode, x.ItemCode, x.Date, x.Issue!)).ToArray()
        };
    }
}

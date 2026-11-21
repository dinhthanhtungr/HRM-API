using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Commons.Searching;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.PLPU;

internal sealed class PLPUAccountingHeaderReader(IReportReadDbContext reports, IPurchaseOrderDbContext purchases)
{
    internal sealed class Row
    {
        public PLPUPoInvoiceSummaryRow View { get; init; } = new();
        public decimal? PoTotalInclVat { get; init; }
    }

    internal IQueryable<Row> Query(PLPUPurchaseOverviewQuery query)
    {
        if (query.CompanyId is not { } company || company == Guid.Empty)
            throw new InvalidOperationException("Current company is required for accounting reports.");
        var orders = purchases.PurchaseOrders.AsNoTracking().Where(p => p.CompanyId == company);
        if (query.From is { } from) orders = orders.Where(p => p.CreateDate >= from.Date);
        if (query.To is { } to) { var end = to.Date.AddDays(1); orders = orders.Where(p => p.CreateDate < end); }
        if (query.PurchaseOrderId is { } id) orders = orders.Where(p => p.PurchaseOrderId == id);
        if (query.SupplierId is { } supplier) orders = orders.Where(p => p.SupplierId == supplier);
        if (query.MaterialId is { } material) orders = orders.Where(p => p.PurchaseOrderDetails.Any(d => d.IsActive && d.MaterialId == material));
        if (!string.IsNullOrWhiteSpace(query.Status)) orders = orders.Where(p => p.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.OrderType)) orders = orders.Where(p => p.OrderType == query.OrderType);
        var search = SmartSearch.For(orders, f => [
            f.Code("purchaseOrderCode", p => p.ExternalId, "Mã đơn mua", "Purchase order code"),
            f.Code("supplierCode", p => p.Supplier != null && p.Supplier.CompanyId == company ? p.Supplier.ExternalId : "", "Mã NCC", "Supplier code"),
            f.Name("supplierName", p => p.Supplier != null && p.Supplier.CompanyId == company ? p.Supplier.SupplierName : "", "Tên NCC", "Supplier name"),
            f.Codes("materialCode", p => p.PurchaseOrderDetails.Where(d => d.IsActive), d => d.MaterialExternalIDSnapshot, "Mã NVL", "Material code"),
            f.Names("materialName", p => p.PurchaseOrderDetails.Where(d => d.IsActive), d => d.MaterialNameSnapshot, "Tên NVL", "Material name")
        ]);
        orders = search.ApplyRequest(orders, query.Keyword, query, out var keyword);
        orders = orders.WhereKeyword(keyword, term => p =>
            (p.ExternalId ?? "").Contains(term.Value)
            || p.PurchaseOrderSnapshot != null && (p.PurchaseOrderSnapshot.SupplierNameSnapshot ?? "").Contains(term.Value)
            || p.PurchaseOrderDetails.Any(d => d.IsActive && ((d.MaterialExternalIDSnapshot ?? "").Contains(term.Value)
                || (d.MaterialNameSnapshot ?? "").Contains(term.Value))));
        return from v in reports.PLPUPoInvoiceSummaries.AsNoTracking().Where(v => v.CompanyId == company)
               join p in orders on v.PurchaseOrderId equals p.PurchaseOrderId
               join s in purchases.PurchaseOrderSnapshots.AsNoTracking()
                   on p.PurchaseOrderSnapshotId equals s.PurchaseOrderSnapshotId into snapshots
               from s in snapshots.DefaultIfEmpty()
               select new Row { View = v, PoTotalInclVat = s == null ? null : s.TotalPrice };
    }

    public async Task<PLPUAccountingSummaryDto> ReadAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct)
        => Calculate(await Query(query).ToListAsync(ct));

    internal static PLPUAccountingSummaryDto Calculate(IReadOnlyList<Row> rows)
    {
        var missing = rows.Count(r => !r.PoTotalInclVat.HasValue);
        var totalPo = rows.Count;
        var withInvoice = rows.Count(r => r.View.InvoiceCount > 0);
        var matched = rows.Count(r => r.View.ReconcileStatus == "MATCHED");
        return new PLPUAccountingSummaryDto
        {
            TotalPo = totalPo,
            SupplierCount = rows.Where(r => r.View.SourceSupplierId.HasValue).Select(r => r.View.SourceSupplierId).Distinct().Count(),
            PoWithoutSupplierCount = rows.Count(r => !r.View.SourceSupplierId.HasValue),
            TotalLines = rows.Sum(r => r.View.PoLineCount), PoQuantity = rows.Sum(r => r.View.PoQty),
            PoAmountExclVat = rows.Sum(r => r.View.PoAmountExclVat),
            PoAmountInclVat = missing == 0 ? rows.Sum(r => r.PoTotalInclVat ?? 0m) : null,
            PoVatAmount = missing == 0 ? rows.Sum(r => r.PoTotalInclVat!.Value - r.View.PoAmountExclVat) : null,
            MissingPoTotalCount = missing, LinkedInvoiceCount = rows.Sum(r => r.View.InvoiceCount),
            PoWithInvoiceCount = withInvoice, PoWithoutInvoiceCount = rows.Count(r => r.View.InvoiceCount == 0),
            MatchedPoCount = matched, PartialPoCount = rows.Count(r => r.View.ReconcileStatus == "PARTIAL"),
            OverPoCount = rows.Count(r => r.View.ReconcileStatus == "OVER"),
            InvoiceQuantity = rows.Sum(r => r.View.ApQty), InvoiceAmountExclVat = rows.Sum(r => r.View.ApAmountExclVat),
            InvoiceVatAmount = rows.Sum(r => r.View.ApVatAmount), InvoiceAmountInclVat = rows.Sum(r => r.View.ApAmountInclVat),
            QuantityDifference = rows.Sum(r => r.View.DiffQty), AmountDifferenceExclVat = rows.Sum(r => r.View.DiffAmountExclVat),
            UninvoicedPoAmountExclVat = rows.Where(r => r.View.InvoiceCount == 0).Sum(r => r.View.PoAmountExclVat),
            PoWithInvoicePercent = totalPo == 0 ? 0 : Math.Round(withInvoice * 100m / totalPo, 2),
            MatchedPoPercent = totalPo == 0 ? 0 : Math.Round(matched * 100m / totalPo, 2),
            PoStatuses = rows.GroupBy(r => r.View.PoStatus).OrderBy(g => g.Key)
                .Select(g => new PLPUAccountingStatusCount(g.Key, g.Count())).ToList()
        };
    }
}

using HRM.Application.Commons.Searching;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.PLPU;

public partial class PLPUPurchaseOverviewReportRepository
{
    internal sealed class SearchRow
    {
        public string Order { get; init; } = "";
        public string SupplierCode { get; init; } = "";
        public string SupplierName { get; init; } = "";
        public string MaterialCode { get; init; } = "";
        public string MaterialName { get; init; } = "";
    }

    internal IQueryable<SearchRow> BuildSearchRows(PLPUPurchaseOverviewQuery query)
    {
        if (query.CompanyId is not {} company || company == Guid.Empty) throw new InvalidOperationException("A current company is required.");
        var suppliers = _context.Suppliers.AsNoTracking().Where(s => s.CompanyId == company);
        var orders = from row in BuildOrderLineQuery(query)
            join supplier in suppliers on row.SupplierId equals supplier.SupplierId into supplierRows
            from supplier in supplierRows.DefaultIfEmpty()
            select new SearchRow { Order = row.POExternalId, SupplierCode = supplier == null ? "" : supplier.ExternalId ?? "",
                SupplierName = row.SupplierName, MaterialCode = row.MaterialCode, MaterialName = row.MaterialName };
        var receipts = from row in BuildReceiptQuery(query)
            join supplier in suppliers on row.SupplierId equals supplier.SupplierId into supplierRows
            from supplier in supplierRows.DefaultIfEmpty()
            select new SearchRow { Order = row.POExternalId, SupplierCode = supplier == null ? "" : supplier.ExternalId ?? "",
                SupplierName = row.SupplierName, MaterialCode = row.ProductCode, MaterialName = row.ProductName };
        return orders.Concat(receipts);
    }

    internal async Task<IReadOnlyList<SmartSearchSuggestion>> SuggestAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct)
    {
        var rows = BuildSearchRows(query);
        var schema = PLPUSmartSearch.Build(rows, x => x.Order, x => x.SupplierCode, x => x.SupplierName, x => x.MaterialCode, x => x.MaterialName);
        return (await schema.SuggestionsAsync<SearchRow>(rows, query, ct))?.SearchSuggestions ?? [];
    }
}

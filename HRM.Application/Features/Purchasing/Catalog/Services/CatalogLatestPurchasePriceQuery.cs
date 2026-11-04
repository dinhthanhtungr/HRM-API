using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Pricing.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal static class CatalogLatestPurchasePriceQuery
{
    internal sealed class Row
    {
        public Guid SupplierId { get; init; }
        public Guid DetailId { get; init; }
        public decimal? Amount { get; init; }
        public DateTime? PriceDate { get; init; }
    }

    // One correlated top-one per supplier, executed as a single SQL query for the current page.
    internal static IQueryable<Row> Build(IPLMDbContext db, Guid companyId, Guid materialId, Guid[] supplierIds)
        => db.Suppliers.AsNoTracking().Where(s => s.CompanyId == companyId && supplierIds.Contains(s.SupplierId))
            .SelectMany(s => s.PurchaseOrders.Where(p => p.CompanyId == companyId &&
                    (p.IsActive ?? true) && (p.Status == null || !PurchaseOrderPriceRules.CanceledStatuses.Contains(p.Status)))
                .SelectMany(p => p.PurchaseOrderDetails.Where(d => d.IsActive && d.MaterialId == materialId &&
                        d.Material.CompanyId == companyId)
                    .Select(d => new Row
                    {
                        SupplierId = s.SupplierId, DetailId = d.PurchaseOrderDetailId,
                        Amount = d.UnitPriceAgreed, PriceDate = p.CreateDate
                    }))
                .OrderByDescending(x => x.PriceDate.HasValue).ThenByDescending(x => x.PriceDate)
                .ThenByDescending(x => x.DetailId).ThenByDescending(x => x.Amount.HasValue)
                .ThenByDescending(x => x.Amount).Take(1));
}

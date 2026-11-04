using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Application.Features.Purchasing.Materials.Services;

internal static class PurchasingMaterialPriceReview
{
    // Each source already selects at most one candidate per material BEFORE validating its price.
    // Join once per source instead of repeating correlated EXISTS over the whole material query.
    internal static IQueryable<Material> Filter(IQueryable<Material> materials, Guid company,
        DateTime cutoff, MaterialPriceReviewStatus status)
    {
        var orders = PurchasingMaterialLatestPriceQuery.PurchaseOrders(materials, company);
        var suppliers = PurchasingMaterialLatestPriceQuery.Suppliers(materials, company);
        var candidates = from material in materials
                         join order in orders on material.MaterialId equals order.MaterialId into orderRows
                         from order in orderRows.DefaultIfEmpty()
                         join supplier in suppliers on material.MaterialId equals supplier.MaterialId into supplierRows
                         from supplier in supplierRows.DefaultIfEmpty()
                         select new
                         {
                             Material = material,
                             OrderAmount = order == null ? (decimal?)null : order.Amount,
                             OrderDate = order == null ? (DateTime?)null : order.PriceDate,
                             SupplierAmount = supplier == null ? (decimal?)null : supplier.Amount,
                             SupplierDate = supplier == null ? (DateTime?)null : supplier.PriceDate
                         };
        // Null explicitly means invalid, including absent LEFT JOIN rows; avoid SQL UNKNOWN in negations.
        var classified = candidates.Select(x => new
        {
            x.Material,
            ValidOrder = x.OrderAmount.HasValue && x.OrderAmount >= 0,
            ValidSupplier = x.SupplierAmount.HasValue && x.SupplierAmount >= 0,
            FreshOrder = x.OrderAmount.HasValue && x.OrderAmount >= 0 && x.OrderDate.HasValue && x.OrderDate >= cutoff,
            FreshSupplier = x.SupplierAmount.HasValue && x.SupplierAmount >= 0 && x.SupplierDate.HasValue && x.SupplierDate >= cutoff
        });
        return (status switch
        {
            MaterialPriceReviewStatus.MissingPrice => classified.Where(x => !x.ValidOrder && !x.ValidSupplier),
            MaterialPriceReviewStatus.UpToDate => classified.Where(x => x.FreshOrder || x.FreshSupplier),
            MaterialPriceReviewStatus.NeedsReview => classified.Where(x => (x.ValidOrder || x.ValidSupplier) && !x.FreshOrder && !x.FreshSupplier),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        }).Select(x => x.Material);
    }

    internal static MaterialPriceReviewStatus Resolve(string source, DateTime? date, DateTime cutoff)
        => source == "Unknown" ? MaterialPriceReviewStatus.MissingPrice :
            !date.HasValue || date.Value < cutoff ? MaterialPriceReviewStatus.NeedsReview : MaterialPriceReviewStatus.UpToDate;
}

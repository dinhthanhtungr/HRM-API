using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Commons.Pricing.Rules;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Application.Features.Purchasing.Materials.Services;

internal static class PurchasingMaterialLatestPriceQuery
{
    internal sealed class Row
    {
        public Guid MaterialId { get; init; }
        public Guid CandidateId { get; init; }
        public decimal? Amount { get; init; }
        public DateTime? PriceDate { get; init; }
        public string? Currency { get; init; }
        public bool IsPreferred { get; init; }

        internal MaterialPriceCandidate Candidate(MaterialPriceSource source) => new()
        {
            MaterialId = MaterialId, CandidateId = CandidateId, CurrentPrice = Amount ?? 0m,
            HasPriceValue = Amount.HasValue, PriceDate = PriceDate, IsPreferred = IsPreferred, PriceSource = source
        };
    }

    internal static IQueryable<Row> PurchaseOrders(IPurchaseOrderDbContext db, Guid companyId, Guid[] ids)
        => PurchaseOrders(db.Materials.AsNoTracking().Where(m => ids.Contains(m.MaterialId)), companyId);

    internal static IQueryable<Row> PurchaseOrders(IQueryable<Material> materials, Guid companyId)
        => materials.Where(m => m.CompanyId == companyId)
            .SelectMany(m => m.PurchaseOrderDetails.Where(d => d.IsActive && d.PurchaseOrder.CompanyId == companyId &&
                    (d.PurchaseOrder.IsActive ?? true) && d.PurchaseOrder.Supplier != null &&
                    d.PurchaseOrder.Supplier.CompanyId == companyId &&
                    (d.PurchaseOrder.Status == null || !PurchaseOrderPriceRules.CanceledStatuses.Contains(d.PurchaseOrder.Status)))
                .OrderByDescending(d => d.PurchaseOrder.CreateDate.HasValue).ThenByDescending(d => d.PurchaseOrder.CreateDate)
                .ThenByDescending(d => d.PurchaseOrderDetailId).ThenByDescending(d => d.UnitPriceAgreed.HasValue)
                .ThenByDescending(d => d.UnitPriceAgreed).Take(1)
                .Select(d => new Row
                {
                    MaterialId = m.MaterialId, CandidateId = d.PurchaseOrderDetailId, Amount = d.UnitPriceAgreed,
                    PriceDate = d.PurchaseOrder.CreateDate, Currency = "VND"
                }));

    internal static IQueryable<Row> Suppliers(IPurchaseOrderDbContext db, Guid companyId, Guid[] ids)
        => Suppliers(db.Materials.AsNoTracking().Where(m => ids.Contains(m.MaterialId)), companyId);

    internal static IQueryable<Row> Suppliers(IQueryable<Material> materials, Guid companyId)
        => materials.Where(m => m.CompanyId == companyId)
            .SelectMany(m => m.MaterialsSuppliers.Where(s => s.IsActive == true && s.Supplier.CompanyId == companyId && s.Supplier.IsActive == true)
                .OrderByDescending(s => (s.UpdatedDate ?? s.CreateDate).HasValue).ThenByDescending(s => s.UpdatedDate ?? s.CreateDate)
                .ThenByDescending(s => s.IsPreferred ?? false).ThenByDescending(s => s.MaterialsSuppliersId)
                .ThenByDescending(s => s.CurrentPrice.HasValue).ThenByDescending(s => s.CurrentPrice).Take(1)
                .Select(s => new Row
                {
                    MaterialId = m.MaterialId, CandidateId = s.MaterialsSuppliersId, Amount = s.CurrentPrice,
                    PriceDate = s.UpdatedDate ?? s.CreateDate, Currency = s.Currency, IsPreferred = s.IsPreferred ?? false
                }));
}

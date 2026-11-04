using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Commons.Pricing.Rules;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetSupplierDetail;

internal static class SupplierActualDeliveryReader
{
    internal sealed class Observation
    {
        public Guid SupplierId { get; init; }
        public Guid PurchaseOrderId { get; init; }
        public DateTime? CreatedAt { get; init; }
        public DateTime? FirstActualDeliveryAt { get; init; }
    }

    internal static IQueryable<Observation> Query(IPLMReadDbContext db, Guid companyId, Guid supplierId)
        => QueryOrders(db, companyId, [supplierId], false);

    internal static IQueryable<Observation> QueryForMaterial(
        IPLMDbContext db, Guid companyId, Guid materialId, Guid[] supplierIds, bool includeInactive)
        => QueryOrders(db, companyId, supplierIds, includeInactive, materialId);

    private static IQueryable<Observation> QueryOrders(
        IPLMDbContext db, Guid companyId, Guid[] supplierIds, bool includeInactive, Guid? materialId = null)
    {
        var inspections = db.QCInputByQCs.AsNoTracking().Where(q =>
            q.WarehouseVoucherDetail.Voucher.CompanyId == companyId &&
            q.WarehouseVoucherDetail.Voucher.WarehouseRequest != null &&
            q.WarehouseVoucherDetail.Voucher.WarehouseRequest.CompanyId == companyId &&
            q.WarehouseVoucherDetail.Voucher.WarehouseRequest.IsActive &&
            q.WarehouseVoucherDetail.Voucher.WarehouseRequest.codeFromRequest != null &&
            (q.WarehouseVoucherDetail.Voucher.VoucherType == (int)WareHouseRequestType.ImportRawMaterial ||
             q.WarehouseVoucherDetail.Voucher.VoucherType == (int)WareHouseRequestType.ImportOther ||
             q.WarehouseVoucherDetail.Voucher.VoucherType == (int)WareHouseRequestType.ImportMaterial));
        return db.Suppliers.AsNoTracking().Where(s => s.CompanyId == companyId &&
                (includeInactive || s.IsActive == true) && supplierIds.Contains(s.SupplierId))
            .SelectMany(s => s.PurchaseOrders, (s, p) => new { s.SupplierId, Order = p })
            .Where(x => x.Order.CompanyId == companyId && x.Order.IsActive == true &&
                (x.Order.Status == null || !PurchaseOrderPriceRules.CanceledStatuses.Contains(x.Order.Status)) &&
                (!materialId.HasValue || x.Order.PurchaseOrderDetails.Any(d => d.IsActive &&
                    d.MaterialId == materialId.Value && d.Material.CompanyId == companyId)))
            .Select(x => new Observation
            {
                SupplierId = x.SupplierId,
                PurchaseOrderId = x.Order.PurchaseOrderId, CreatedAt = x.Order.CreateDate,
                FirstActualDeliveryAt = inspections.Where(q => x.Order.ExternalId != null && x.Order.ExternalId.Trim() != "" &&
                    q.WarehouseVoucherDetail.Voucher.WarehouseRequest!.codeFromRequest!.Trim() == x.Order.ExternalId.Trim())
                    .Select(q => (DateTime?)q.CreatedDate).Min()
            });
    }

    internal static IQueryable<SupplierActualDeliveryDto> Aggregate(IQueryable<Observation> query) => query.GroupBy(x => 1).Select(g => new SupplierActualDeliveryDto
    {
        OrderCount = g.Count(),
        SampleCount = g.Count(x => x.CreatedAt != null && x.FirstActualDeliveryAt != null && x.FirstActualDeliveryAt >= x.CreatedAt),
        MissingDateCount = g.Count(x => x.CreatedAt == null || x.FirstActualDeliveryAt == null),
        InvalidDateCount = g.Count(x => x.CreatedAt != null && x.FirstActualDeliveryAt != null && x.FirstActualDeliveryAt < x.CreatedAt),
        AverageDays = g.Average(x => x.CreatedAt != null && x.FirstActualDeliveryAt != null && x.FirstActualDeliveryAt >= x.CreatedAt
            ? (double?)(x.FirstActualDeliveryAt.Value - x.CreatedAt.Value).TotalDays : null)
    });

    internal static async Task PopulateAsync(IPLMReadDbContext db, Guid companyId, Guid supplierId, SupplierDetailDto supplier, CancellationToken ct)
    {
        var query = Query(db, companyId, supplierId);
        supplier.ActualDelivery = await Aggregate(query).FirstOrDefaultAsync(ct) ?? new();
        Round(supplier.ActualDelivery);
        if (supplier.Materials.Items.Count == 0) return;

        // Chỉ lấy các cặp PO/vật tư của trang hiện tại; nhiều dòng cùng vật tư trên một PO chỉ đếm một lần.
        var ids = supplier.Materials.Items.Select(x => x.MaterialId).Distinct().ToArray();
        var pairs = db.Suppliers.AsNoTracking().Where(s => s.CompanyId == companyId && s.IsActive == true && s.SupplierId == supplierId)
            .SelectMany(s => s.PurchaseOrders).Where(p => p.CompanyId == companyId && p.IsActive == true &&
                (p.Status == null || !PurchaseOrderPriceRules.CanceledStatuses.Contains(p.Status)))
            .SelectMany(p => p.PurchaseOrderDetails.Where(d => d.IsActive && ids.Contains(d.MaterialId)
                && d.Material.CompanyId == companyId && d.Material.IsActive == true)
                .Select(d => new { p.PurchaseOrderId, d.MaterialId })).Distinct();
        var observations = await query.Join(pairs, o => o.PurchaseOrderId, p => p.PurchaseOrderId,
            (o, p) => new { p.MaterialId, o.CreatedAt, o.FirstActualDeliveryAt }).ToListAsync(ct);
        foreach (var material in supplier.Materials.Items)
        {
            var rows = observations.Where(x => x.MaterialId == material.MaterialId).Select(x => new Observation
            { CreatedAt = x.CreatedAt, FirstActualDeliveryAt = x.FirstActualDeliveryAt }).AsQueryable();
            material.ActualDelivery = Aggregate(rows).FirstOrDefault() ?? new();
            Round(material.ActualDelivery);
        }
    }

    internal static void Round(SupplierActualDeliveryDto result)
    {
        if (result.AverageDays.HasValue) result.AverageDays = Math.Round(result.AverageDays.Value, 2, MidpointRounding.AwayFromZero);
    }
}

using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Application.Features.PLM.Materials.Queries.GetSupplierDetail;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public async Task<PagedResult<CatalogSupplierLinkDto>?> GetMaterialSupplierLinksAsync(Guid materialId, CatalogListQuery request, CancellationToken ct)
    {
        var companyId = RequireReadContext();
        if (!permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders))
            throw new UnauthorizedAccessException("Không có quyền xem giá NCC của vật tư.");
        if (!await db.Materials.AnyAsync(x => x.MaterialId == materialId && x.CompanyId == companyId && (request.IncludeInactive || x.IsActive == true), ct))
            return null;
        var query = db.MaterialsSuppliers.AsNoTracking().Where(x => x.MaterialId == materialId && x.Material.CompanyId == companyId &&
            x.Supplier.CompanyId == companyId && (request.IncludeInactive || x.IsActive == true && x.Supplier.IsActive == true))
            .WhereKeyword(request.Keyword, term => x =>
                EF.Functions.ILike(x.Supplier.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Supplier.SupplierName ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.IsActive == true).ThenByDescending(x => x.IsPreferred == true)
            .ThenBy(x => x.Supplier.ExternalId).ThenBy(x => x.MaterialsSuppliersId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize)
            .Select(x => new CatalogSupplierLinkDto
            {
                MaterialsSupplierId = x.MaterialsSuppliersId, SupplierId = x.SupplierId,
                SupplierCode = x.Supplier.ExternalId, SupplierName = x.Supplier.SupplierName,
                SupplierIsActive = x.Supplier.IsActive == true, IsActive = x.IsActive == true,
                IsPreferred = x.IsPreferred == true, CurrentPrice = x.CurrentPrice, Currency = x.Currency,
                MinDeliveryDays = x.MinDeliveryDays, UpdatedDate = x.UpdatedDate,
                LatestPrice = new CatalogLatestPriceDto
                {
                    Amount = x.CurrentPrice, Currency = x.Currency,
                    PriceDate = x.UpdatedDate ?? x.CreateDate
                }
            }).ToListAsync(ct);
        if (items.Count > 0)
        {
            // Chỉ đọc lịch sử của các NCC trên trang; Any giữ mỗi PO là một mẫu dù lặp dòng NVL.
            var supplierIds = items.Select(x => x.SupplierId).Distinct().ToArray();
            var purchasePrices = await CatalogLatestPurchasePriceQuery.Build(db, companyId, materialId, supplierIds).ToListAsync(ct);
            var purchasePriceMap = purchasePrices.ToDictionary(x => x.SupplierId);
            foreach (var item in items)
            {
                purchasePriceMap.TryGetValue(item.SupplierId, out var po);
                var purchaseCandidate = po is null ? null : new MaterialPriceCandidate
                {
                    MaterialId = materialId, CandidateId = po.DetailId,
                    CurrentPrice = po.Amount ?? 0m, HasPriceValue = po.Amount.HasValue,
                    PriceDate = po.PriceDate, PriceSource = MaterialPriceSource.PurchaseOrder
                };
                var supplierCandidate = item.IsActive ? new MaterialPriceCandidate
                {
                    MaterialId = materialId, CandidateId = item.MaterialsSupplierId,
                    CurrentPrice = item.CurrentPrice ?? 0m, HasPriceValue = item.CurrentPrice.HasValue,
                    PriceDate = item.LatestPrice.PriceDate, PriceSource = MaterialPriceSource.MaterialSupplier,
                    IsPreferred = item.IsPreferred
                } : null;
                var selected = MaterialLatestPriceSelector.Select(materialId, purchaseCandidate, supplierCandidate);
                item.LatestPrice = new CatalogLatestPriceDto
                {
                    Amount = selected.PriceSource == MaterialPriceSource.Unknown ? null : selected.CurrentPrice,
                    Currency = selected.PriceSource == MaterialPriceSource.PurchaseOrder ? "VND" :
                        selected.PriceSource == MaterialPriceSource.MaterialSupplier ? item.Currency : null,
                    PriceDate = selected.PriceDate, Source = selected.PriceSource.ToString()
                };
            }
            var observations = await SupplierActualDeliveryReader.QueryForMaterial(
                db, companyId, materialId, supplierIds, request.IncludeInactive).ToListAsync(ct);
            var metrics = observations.GroupBy(x => x.SupplierId).ToDictionary(x => x.Key,
                x => SupplierActualDeliveryReader.Aggregate(x.AsQueryable()).Single());
            foreach (var item in items)
            {
                if (metrics.TryGetValue(item.SupplierId, out var metric))
                {
                    SupplierActualDeliveryReader.Round(metric);
                    item.ActualDelivery = metric;
                }
            }
        }
        return new(items, count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }
}

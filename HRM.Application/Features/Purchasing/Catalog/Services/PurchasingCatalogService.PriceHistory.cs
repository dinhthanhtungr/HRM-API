using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public async Task<PagedResult<CatalogMaterialPriceHistoryDto>?> GetMaterialPriceHistoryAsync(
        Guid materialId, Guid? materialsSupplierId, CatalogListQuery request, CancellationToken ct)
    {
        var companyId = RequireReadContext();
        if (!permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders))
            throw new UnauthorizedAccessException("Không có quyền xem lịch sử giá vật tư.");

        if (!await db.Materials.AsNoTracking().AnyAsync(x => x.MaterialId == materialId &&
                x.CompanyId == companyId && (request.IncludeInactive || x.IsActive == true), ct))
            return null;

        var links = db.MaterialsSuppliers.AsNoTracking().Where(x => x.MaterialId == materialId &&
            x.Material.CompanyId == companyId && x.Supplier.CompanyId == companyId &&
            (request.IncludeInactive || x.IsActive == true && x.Supplier.IsActive == true));
        if (materialsSupplierId.HasValue)
        {
            links = links.Where(x => x.MaterialsSuppliersId == materialsSupplierId.Value);
            if (!await links.AnyAsync(ct)) return null;
        }

        var query = from history in db.PriceHistories.AsNoTracking()
                    join link in links on history.MaterialsSuppliersId equals link.MaterialsSuppliersId
                    select new { History = history, Link = link };
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.History.CreateDate.HasValue)
            .ThenByDescending(x => x.History.CreateDate).ThenByDescending(x => x.History.PriceHistoryId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize)
            .Select(x => new CatalogMaterialPriceHistoryDto
            {
                PriceHistoryId = x.History.PriceHistoryId,
                MaterialsSupplierId = x.Link.MaterialsSuppliersId,
                SupplierId = x.Link.SupplierId, SupplierCode = x.Link.Supplier.ExternalId,
                SupplierName = x.Link.Supplier.SupplierName,
                OldPrice = x.History.OldPrice, Currency = x.History.Currency,
                RecordedAt = x.History.CreateDate,
                RecordedByEmployeeId = x.History.CreatedByNavigation != null &&
                    x.History.CreatedByNavigation.CompanyId == companyId ? x.History.CreatedBy : null,
                RecordedByName = x.History.CreatedByNavigation != null &&
                    x.History.CreatedByNavigation.CompanyId == companyId ? x.History.CreatedByNavigation.FullName : null
            }).ToListAsync(ct);
        return new(items, totalCount, request.NormalizedPageNumber, request.NormalizedPageSize);
    }
}

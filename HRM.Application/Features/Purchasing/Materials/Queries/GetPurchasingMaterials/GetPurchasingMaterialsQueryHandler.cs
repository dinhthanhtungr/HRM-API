using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Application.Features.Purchasing.Materials.Dtos;
using HRM.Application.Features.Purchasing.Materials.Services;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;

internal sealed class GetPurchasingMaterialsQueryHandler(
    IPurchaseOrderDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions, IDateTimeProvider clock)
    : IRequestHandler<GetPurchasingMaterialsQuery, PagedResult<PurchasingMaterialSummaryDto>>
{
    public async Task<PagedResult<PurchasingMaterialSummaryDto>> Handle(GetPurchasingMaterialsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !(PurchaseOrderAccess.CanManage(permissions) || permissions.HasPermission(ApplicationPermissions.Purchasing.ManageCatalog)) || user.CompanyId is not { } companyId || companyId == Guid.Empty)
            throw new UnauthorizedAccessException("Không có quyền xem danh mục vật tư mua hàng.");

        var query = PurchasingMaterialReadModel.Filter(db.Materials, companyId, request, includeKeyword: false);
        var canViewPrices = PurchaseOrderAccess.CanManage(permissions);
        var cutoff = clock.Now.AddDays(-request.NormalizedStaleAfterDays);
        if (request.PriceStatus.HasValue)
        {
            if (!Enum.IsDefined(request.PriceStatus.Value)) throw new ArgumentException("Trạng thái giá không hợp lệ.");
            if (!canViewPrices) throw new UnauthorizedAccessException("Không có quyền lọc theo giá NVL.");
            query = PurchasingMaterialPriceReview.Filter(query, companyId, cutoff, request.PriceStatus.Value);
        }
        var (items, total, suggestions) = await PurchasingMaterialCatalogPage.ReadAsync(db, query, companyId, request, ct);
        if (suggestions is not null)
            return new([], 0, 1, 10) { SearchSuggestions = suggestions };
        foreach (var item in items) PurchasingMaterialReadModel.NormalizeAvailability(item);
        if (items.Count > 0 && PurchaseOrderAccess.CanManage(permissions))
        {
            var ids = items.Where(x => !x.IsSemiFinished).Select(x => x.MaterialId).ToArray();
            var purchasePrices = (await PurchasingMaterialLatestPriceQuery.PurchaseOrders(db, companyId, ids).ToListAsync(ct))
                .ToDictionary(x => x.MaterialId);
            var supplierPrices = (await PurchasingMaterialLatestPriceQuery.Suppliers(db, companyId, ids).ToListAsync(ct))
                .ToDictionary(x => x.MaterialId);
            foreach (var item in items)
            {
                purchasePrices.TryGetValue(item.MaterialId, out var po);
                supplierPrices.TryGetValue(item.MaterialId, out var supplier);
                var selected = MaterialLatestPriceSelector.Select(item.MaterialId,
                    po?.Candidate(MaterialPriceSource.PurchaseOrder), supplier?.Candidate(MaterialPriceSource.MaterialSupplier));
                item.LatestPrice = new CatalogLatestPriceDto
                {
                    Amount = selected.PriceSource == MaterialPriceSource.Unknown ? null : selected.CurrentPrice,
                    Currency = selected.PriceSource == MaterialPriceSource.PurchaseOrder ? po?.Currency :
                        selected.PriceSource == MaterialPriceSource.MaterialSupplier ? supplier?.Currency : null,
                    PriceDate = selected.PriceDate, Source = selected.PriceSource.ToString()
                };
                item.ReviewStatus = PurchasingMaterialPriceReview.Resolve(item.LatestPrice.Source, item.LatestPrice.PriceDate, cutoff);
            }
        }
        return new(items, total, request.NormalizedPageNumber, request.NormalizedPageSize);
    }
}

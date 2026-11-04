using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Purchasing.Catalog.Dtos;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

public interface IPurchasingCatalogService
{
    Task<PagedResult<CatalogMaterialPriceHistoryDto>?> GetMaterialPriceHistoryAsync(Guid materialId, Guid? materialsSupplierId, CatalogListQuery query, CancellationToken ct);
    Task<PagedResult<CatalogSupplierSummaryDto>> GetSuppliersAsync(CatalogListQuery query, CancellationToken ct);
    Task<CatalogSupplierDetailDto?> GetSupplierAsync(Guid id, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<CatalogCategoryDto>> GetCategoriesAsync(CancellationToken ct, HRM.Domain.Enums.Category.CategoryTypes? type = null);
    Task<IReadOnlyList<CatalogUnitDto>> GetUnitsAsync(CancellationToken ct);
    Task<PagedResult<CatalogSupplierLinkDto>?> GetMaterialSupplierLinksAsync(Guid materialId, CatalogListQuery query, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SaveSupplierAsync(Guid? id, CatalogSupplierWriteRequest request, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SaveMaterialAsync(Guid? id, CatalogMaterialWriteRequest request, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SetSupplierActiveAsync(Guid id, bool active, DateTime? expected, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SetMaterialActiveAsync(Guid id, bool active, DateTime? expected, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SaveContactAsync(Guid supplierId, Guid? id, CatalogContactWriteRequest request, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SaveAddressAsync(Guid supplierId, Guid? id, CatalogAddressWriteRequest request, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SetContactActiveAsync(Guid supplierId, Guid id, bool active, DateTime? expected, CancellationToken ct);
    Task<OperationResult<CatalogSavedDto>> SetAddressActiveAsync(Guid supplierId, Guid id, bool active, DateTime? expected, CancellationToken ct);
}

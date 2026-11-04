using HRM.Application.Commons.Pagination;
namespace HRM.Application.Features.Purchasing.Catalog.Dtos;

public sealed class CatalogListQuery : PaginationQuery
{
    public bool IncludeInactive { get; init; }
}

public sealed record CatalogSupplierSummaryDto(Guid SupplierId, string? ExternalId, string? SupplierName, string? Phone, bool IsActive, DateTime? UpdatedDate);
public sealed record CatalogCategoryDto(Guid CategoryId, string? ExternalId, string? Name, string? Types);
public sealed record CatalogUnitDto(Guid UnitId, string? Name, string? Symbol);

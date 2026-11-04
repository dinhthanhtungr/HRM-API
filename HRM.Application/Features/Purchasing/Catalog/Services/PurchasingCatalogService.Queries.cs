using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public async Task<PagedResult<CatalogSupplierSummaryDto>> GetSuppliersAsync(CatalogListQuery request, CancellationToken ct)
    {
        var companyId = RequireReadContext();
        var query = db.Suppliers.AsNoTracking().Where(x => x.CompanyId == companyId && (request.IncludeInactive || x.IsActive == true))
            .WhereKeyword(request.Keyword, term =>
            {
                if (term.StartsWith("VT") || term.StartsWith("NVL_"))
                    return x => x.MaterialsSuppliers.Any(link =>
                        link.Material.CompanyId == companyId &&
                        (request.IncludeInactive || (link.IsActive == true && link.Material.IsActive == true)) &&
                        EF.Functions.ILike(link.Material.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));

                return x =>
                EF.Functions.ILike(x.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.SupplierName ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Phone ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.TaxNumber ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter);
            });
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedDate.HasValue).ThenByDescending(x => x.CreatedDate)
            .ThenBy(x => x.SupplierName).ThenBy(x => x.SupplierId)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize)
            .Select(x => new CatalogSupplierSummaryDto(x.SupplierId, x.ExternalId, x.SupplierName, x.Phone, x.IsActive == true, x.UpdatedDate)).ToListAsync(ct);
        return new(rows, count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }

    public async Task<CatalogSupplierDetailDto?> GetSupplierAsync(Guid id, bool includeInactive, CancellationToken ct)
    {
        var companyId = RequireReadContext();
        return await db.Suppliers.AsNoTracking().Where(x => x.SupplierId == id && x.CompanyId == companyId && (includeInactive || x.IsActive == true))
            .Select(x => new CatalogSupplierDetailDto
            {
                SupplierId = x.SupplierId, ExternalId = x.ExternalId, IsActive = x.IsActive == true,
                MaterialCount = x.MaterialsSuppliers.Where(link => link.IsActive == true &&
                    link.Material.IsActive == true && link.Material.CompanyId == companyId)
                    .Select(link => link.MaterialId).Distinct().Count(),
                CreatedDate = x.CreatedDate, UpdatedDate = x.UpdatedDate, CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy,
                SupplierName = x.SupplierName,
                RegistrationNumber = x.RegistrationNumber,
                RegistrationAddress = x.RegistrationAddress,
                TaxNumber = x.TaxNumber,
                Phone = x.Phone,
                Website = x.Website,
                Note = x.Note,
                IssuedPlace = x.IssuedPlace,
                FaxNumber = x.FaxNumber,
                Priority = x.Priority, IssueDate = x.IssueDate,
                Contacts = x.SupplierContacts.OrderByDescending(c => c.IsActive == true).ThenByDescending(c => c.IsPrimary == true).ThenBy(c => c.ContactId)
                    .Select(c => new CatalogContactDto
                    {
                        ContactId = c.ContactId, IsActive = c.IsActive == true, IsPrimary = c.IsPrimary == true,
                        FirstName = c.FirstName,
                        LastName = c.LastName,
                        Gender = c.Gender,
                        Phone = c.Phone,
                        Email = c.Email,
                    }).ToList(),
                Addresses = x.SupplierAddresses.OrderByDescending(a => a.IsActive == true).ThenByDescending(a => a.IsPrimary == true).ThenBy(a => a.AddressId)
                    .Select(a => new CatalogAddressDto
                    {
                        AddressId = a.AddressId, IsActive = a.IsActive == true, IsPrimary = a.IsPrimary == true,
                        AddressLine = a.AddressLine,
                        City = a.City,
                        District = a.District,
                        Province = a.Province,
                        Country = a.Country,
                        PostalCode = a.PostalCode,
                    }).ToList()
            }).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogCategoryDto>> GetCategoriesAsync(CancellationToken ct, HRM.Domain.Enums.Category.CategoryTypes? type = null)
    {
        // MFG needs names/IDs only to group formula rows, not purchasing-management access.
        var canReadMfgCategories = user.IsAuthenticated &&
            permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders) &&
            permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials);
        var companyId = canReadMfgCategories && user.CompanyId is { } scopedCompany && scopedCompany != Guid.Empty
            ? scopedCompany : RequireReadContext();
        var query = db.Categories.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive == true);
        if (type.HasValue)
        {
            if (!Enum.IsDefined(type.Value)) throw new ArgumentOutOfRangeException(nameof(type));
            var categoryType = type.Value.ToString();
            query = query.Where(x => x.Types == categoryType);
        }
        return await query
            .OrderBy(x => x.Name).ThenBy(x => x.CategoryId)
            .Select(x => new CatalogCategoryDto(x.CategoryId, x.ExternalId, x.Name, x.Types)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogUnitDto>> GetUnitsAsync(CancellationToken ct)
    {
        RequireReadContext();
        // Unit là danh mục tham chiếu dùng chung, model không có CompanyId.
        return await db.Units.AsNoTracking().Where(x => x.IsActive == true)
            .OrderBy(x => x.Name).ThenBy(x => x.UnitId)
            .Select(x => new CatalogUnitDto(x.UnitId, x.Name, x.Symbol)).ToListAsync(ct);
    }
}

using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Purchasing.Materials.Dtos;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Materials;
using HRM.Application.Features.PLM.Materials.Dtos.PriceReview;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Materials.Services;

internal static class PurchasingMaterialCatalogPage
{
    private sealed class Key
    {
        public Guid Id { get; init; }
        public string? Code { get; init; }
        public string? Name { get; init; }
        public DateTime? Created { get; init; }
        public bool Product { get; init; }
        public string? CustomCode { get; init; }
        public string? Barcode { get; init; }
    }

    public static async Task<(List<PurchasingMaterialSummaryDto> Items, int Count, IReadOnlyList<SmartSearchSuggestion>? Suggestions)> ReadAsync(IPurchaseOrderDbContext db,
        IQueryable<Material> materials, Guid company, GetPurchasingMaterialsQuery request, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking().Where(x => x.CompanyId == company && (request.IncludeInactive || x.IsActive)
            && x.SampleRequests.Any(s => s.IsActive && s.CompanyId == company && s.RequestType == "SemiFinished"));
        if (request.CategoryId.HasValue) products = products.Where(x => x.CategoryId == request.CategoryId.Value);
        if (request.SupplierId.HasValue || request.PurchaseStatus.HasValue && request.PurchaseStatus != MaterialPurchaseStatus.Available
            || request.PriceStatus.HasValue && request.PriceStatus != MaterialPriceReviewStatus.MissingPrice)
            products = products.Where(x => false);
        var keys = materials.Select(x => new Key { Id = x.MaterialId, Code = x.ExternalId, Name = x.Name, Created = x.CreatedDate,
                Product = false, CustomCode = x.CustomCode, Barcode = x.Barcode })
            .Concat(products.Select(x => new Key { Id = x.ProductId, Code = x.ColourCode, Name = x.Name, Created = x.CreatedDate,
                Product = true, CustomCode = x.Code, Barcode = null }));
        var smartSearch = SmartSearch.For(keys, fields => [
            fields.Code("materialCode", x => x.Code, "Mã NVL", "Material code"),
            fields.Name("materialName", x => x.Name, "Tên NVL", "Material name"),
            fields.Codes("supplierCode", x => db.MaterialsSuppliers.Where(link => !x.Product && link.MaterialId == x.Id &&
                link.IsActive == true && link.Supplier.CompanyId == company && link.Supplier.IsActive == true),
                link => link.Supplier.ExternalId, "Mã NCC", "Supplier code"),
            fields.Names("supplierName", x => db.MaterialsSuppliers.Where(link => !x.Product && link.MaterialId == x.Id &&
                link.IsActive == true && link.Supplier.CompanyId == company && link.Supplier.IsActive == true),
                link => link.Supplier.SupplierName, "Tên NCC", "Supplier name")
        ]);
        keys = smartSearch.ApplyRequest(keys, request.Keyword, request, out var smartKeyword)
            .WhereKeyword(smartKeyword, term => x =>
                EF.Functions.ILike(x.Code ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.CustomCode ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Barcode ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
        if (await smartSearch.SuggestionsAsync<PurchasingMaterialSummaryDto>(keys, request, ct) is { } suggestions)
            return ([], 0, suggestions.SearchSuggestions);
        var count = await keys.CountAsync(ct);
        var ordered = request.NormalizedSortBy?.ToLowerInvariant() switch
        {
            "name" => request.SortDescending ? keys.OrderByDescending(x => x.Name) : keys.OrderBy(x => x.Name),
            "createddate" => request.SortDescending ? keys.OrderByDescending(x => x.Created) : keys.OrderBy(x => x.Created),
            _ => request.SortDescending ? keys.OrderByDescending(x => x.Code) : keys.OrderBy(x => x.Code)
        };
        var page = await ordered.ThenBy(x => x.Id).Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize).ToListAsync(ct);
        var materialIds = page.Where(x => !x.Product).Select(x => x.Id).ToArray();
        var productIds = page.Where(x => x.Product).Select(x => x.Id).ToArray();
        var items = await materials.Where(x => materialIds.Contains(x.MaterialId)).Select(PurchasingMaterialReadModel.Summary(company)).ToListAsync(ct);
        var internals = await products.Where(x => productIds.Contains(x.ProductId)).Select(x => new
        {
            x.ProductId, x.ColourCode, x.Code, x.Name, x.CategoryId,
            CategoryName = x.Category != null && x.Category.CompanyId == company ? x.Category.Name : null, x.Unit, x.Weight, x.IsActive,
            Package = x.SampleRequests.Where(s => s.CompanyId == company && s.IsActive && s.RequestType == "SemiFinished")
                .OrderByDescending(s => s.CreatedDate).Select(s => s.Package).FirstOrDefault()
        }).ToListAsync(ct);
        items.AddRange(internals.Select(x => new PurchasingMaterialSummaryDto
        {
            MaterialId = x.ProductId, ExternalId = x.ColourCode, CustomCode = x.Code, Name = x.Name,
            CategoryId = x.CategoryId, CategoryName = x.CategoryName, Unit = x.Unit, Weight = x.Weight, Package = x.Package,
            IsActive = x.IsActive, IsSemiFinished = true
        }));
        var map = items.ToDictionary(x => (x.MaterialId, x.IsSemiFinished));
        return (page.Where(x => map.ContainsKey((x.Id, x.Product))).Select(x => map[(x.Id, x.Product)]).ToList(), count, null);
    }
}

using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

internal sealed class GetMaterialEquivalencesQueryHandler(
    IPLMReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions, ISender sender)
    : IRequestHandler<GetMaterialEquivalencesQuery, PagedResult<MaterialEquivalenceDto>?>
{
    public async Task<PagedResult<MaterialEquivalenceDto>?> Handle(GetMaterialEquivalencesQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !ApplicationRoleSets.DevAndQA.MaterialPlanningViewers.Any(user.IsInRole) || user.CompanyId is not Guid companyId || companyId == Guid.Empty ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials)) return null;
        if (request.StockTypes.Any(x => !Enum.IsDefined(x))) throw new ArgumentException("Invalid stock type.");

        // TP is the sample request code; warehouse stock is keyed by Product.ColourCode.
        var productQuery = db.Products.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && x.SampleRequests.Any(s =>
                s.CompanyId == companyId && s.IsActive && s.ExternalId.StartsWith("TP_")))
            .Where(x => EF.Functions.ILike(x.Name ?? "", "%băm%") || EF.Functions.ILike(x.Name ?? "", "%bam%") ||
                EF.Functions.ILike(x.Name ?? "", "%nghiền%") || EF.Functions.ILike(x.Name ?? "", "%nghien%") ||
                EF.Functions.ILike(x.Name ?? "", "%loãng%") || EF.Functions.ILike(x.Name ?? "", "%loang%"));
        var search = MaterialEquivalenceSearch.Build(productQuery, companyId);
        productQuery = search.ApplyRequest(productQuery, request.Keyword, request, out var keyword);
        if (request.SuggestionKeyword is not null) productQuery = search.ApplyAnyKeyword(productQuery, request.SuggestionKeyword);
        var products = await productQuery.WhereKeyword(keyword, term => x =>
                EF.Functions.ILike(x.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.ColourCode ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                x.Formulas.Any(f => f.CompanyId == companyId && f.IsActive && f.FormulaMaterials.Any(m =>
                    m.IsActive && m.MaterialId != null && m.Material.CompanyId == companyId && m.Material.IsActive == true &&
                    (EF.Functions.ILike(m.Material.ExternalId ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                     EF.Functions.ILike(m.Material.Name ?? "", term.ContainsPattern, PostgresSearchPattern.EscapeCharacter)))) ||
                x.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive &&
                    (EF.Functions.ILike(s.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter) ||
                     (s.Formula != null && s.Formula.IsActive && s.Formula.CompanyId == companyId &&
                      EF.Functions.ILike(s.Formula.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter)))))
            .OrderBy(x => x.ColourCode).ThenBy(x => x.ProductId)
            .Select(x => new {
                x.ProductId, Name = x.Name ?? "", ColourCode = x.ColourCode ?? "",
                ProductCode = x.SampleRequests.Where(s => s.CompanyId == companyId && s.IsActive && s.ExternalId.StartsWith("TP_"))
                    .OrderByDescending(s => s.CreatedDate).ThenBy(s => s.SampleRequestId).Select(s => s.ExternalId).First(),
                FormulaId = x.SampleRequests.Where(s => s.CompanyId == companyId && s.IsActive && s.ExternalId.StartsWith("TP_") && s.FormulaId != null)
                    .OrderByDescending(s => s.CreatedDate).ThenBy(s => s.SampleRequestId).Select(s => s.FormulaId).FirstOrDefault(),
            }).ToListAsync(ct);
        var ids = products.Select(x => x.ProductId).ToArray();
        var formulas = await db.Formulas.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive && ids.Contains(x.ProductId))
            .Select(x => new { x.FormulaId, x.ProductId, x.ExternalId, x.IsSelect }).ToListAsync(ct);
        var formulaIds = formulas.Select(x => x.FormulaId).ToArray();
        // Count every referenced NVL, including one no longer eligible as a candidate.
        // A mixed formula must not appear to contain one NVL just because another was disabled.
        var materialCounts = await db.FormulaMaterials.AsNoTracking()
            .Where(x => formulaIds.Contains(x.FormulaId) && x.IsActive && x.MaterialId != null &&
                x.itemType == HRM.Domain.Enums.Formulas.ItemType.Material)
            .GroupBy(x => x.FormulaId)
            .Select(x => new { FormulaId = x.Key, Count = x.Select(m => m.MaterialId).Distinct().Count() })
            .ToDictionaryAsync(x => x.FormulaId, x => x.Count, ct);
        var ingredients = await db.FormulaMaterials.AsNoTracking().Where(x => formulaIds.Contains(x.FormulaId) && x.IsActive &&
                x.MaterialId != null && x.itemType == HRM.Domain.Enums.Formulas.ItemType.Material && x.Material.CompanyId == companyId && x.Material.IsActive == true &&
                x.Material.Category.CompanyId == companyId && x.Material.Category.IsActive == true && x.Material.Category.Types == "NVL")
            .Select(x => new { x.FormulaId, Material = new EquivalentMaterialDto(x.Material.MaterialId,
                x.Material.ExternalId ?? "", x.Material.Name ?? "", x.Material.CategoryId, x.Material.Unit) }).ToListAsync(ct);

        var rows = new List<MaterialEquivalenceDto>();
        foreach (var product in products)
        {
            var type = MaterialEquivalenceRules.ProcessingType(product.Name);
            if (type is null) continue;
            if (!string.IsNullOrEmpty(request.ProcessingType) && type != request.ProcessingType) continue;
            var choices = formulas.Where(x => x.ProductId == product.ProductId).ToArray();
            var selected = choices.Where(x => x.IsSelect).ToArray();
            var formula = selected.Length == 1 ? selected[0] : selected.Length > 1 ? null :
                choices.FirstOrDefault(x => x.FormulaId == product.FormulaId) ?? (choices.Length == 1 ? choices[0] : null);
            var candidates = formula is null ? [] : MaterialEquivalenceRules.Candidates(type,
                ingredients.Where(x => x.FormulaId == formula.FormulaId).Select(x => x.Material));
            if (!MaterialEquivalenceRules.IncludesFormula(type,
                formula is null ? 0 : materialCounts.GetValueOrDefault(formula.FormulaId), candidates.Count)) continue;
            var status = formula is null ? (choices.Length == 0 ? "MissingFormula" : "AmbiguousFormula") :
                candidates.Count == 1 ? "Matched" : candidates.Count == 0 ? "MissingMaterial" : "AmbiguousMaterial";
            rows.Add(new(product.ProductId, product.ProductCode, product.ColourCode, product.Name, type,
                formula?.FormulaId, formula?.ExternalId, status, candidates, [], []));
        }

        rows = MaterialEquivalenceSearch.Apply(rows, request);
        // Without inventory filters, only query inventory for the visible page.
        var hasStockFilters = request.StockTypes.Length > 0 || request.OnlyAvailableLeZero || request.AvailableMax.HasValue ||
            request.ProductOnHandMin.HasValue || request.ProductOnHandMax.HasValue;
        if (!hasStockFilters && request.SuggestionKeyword is not null)
            return new([], 0, 1, 10) { SearchSuggestions = MaterialEquivalenceSearch.Suggest(rows, request) };
        var inventoryRows = hasStockFilters ? rows : rows.Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize).ToList();
        var stocks = new List<StockAvailableDto>();
        var canViewDefective = permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots);
        var codes = inventoryRows.Select(x => x.ColourCode).Concat(inventoryRows.SelectMany(x => x.Candidates.Select(m => m.Code)))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        foreach (var batch in codes.Chunk(20))
        {
            var stock = await sender.Send(new GetStockAvailableQuery {
                ExactCodes = batch, StockTypes =
                    canViewDefective ? [StockType.FinishedGood, StockType.DefectiveFinishedGood, StockType.RawMaterial, StockType.DefectiveRawMaterial]
                        : [StockType.FinishedGood, StockType.RawMaterial],
                PageSize = 100
            }, ct);
            if (!stock.Success || stock.Data is null) throw new InvalidOperationException("Unable to load stock available.");
            stocks.AddRange(stock.Data.Items);
        }
        var result = inventoryRows.Select(row => row with {
            ProductStocks = stocks.Where(x => x.Code == row.ColourCode && (x.StockType == StockType.FinishedGood || x.StockType == StockType.DefectiveFinishedGood)).ToArray(),
            MaterialStocks = stocks.Where(x => row.Candidates.Any(m => m.Code == x.Code) && (x.StockType == StockType.RawMaterial || x.StockType == StockType.DefectiveRawMaterial)).ToArray()
        }).Where(row => MatchesStock(row, request)).Select(row => row with {
            ProductStocks = row.ProductStocks.Where(x => request.StockTypes.Length == 0 || request.StockTypes.Contains(x.StockType)).ToArray(),
            MaterialStocks = row.MaterialStocks.Where(x => request.StockTypes.Length == 0 || request.StockTypes.Contains(x.StockType)).ToArray()
        }).ToArray();
        if (request.SuggestionKeyword is not null)
            return new([], 0, 1, 10) { SearchSuggestions = MaterialEquivalenceSearch.Suggest(result, request) };
        return new(hasStockFilters ? result.Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize).ToArray() : result,
            hasStockFilters ? result.Length : rows.Count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }

    private static bool MatchesStock(MaterialEquivalenceDto row, GetMaterialEquivalencesQuery request)
    {
        // Keep normal and defective stock separate. The range is for normal TP stock only.
        var productOnHand = row.ProductStocks.Where(x => x.StockType == StockType.FinishedGood).Sum(x => x.TotalOnHandKg);
        if (!MaterialEquivalenceRules.MatchesProductOnHand(productOnHand, request.ProductOnHandMin, request.ProductOnHandMax)) return false;
        var stocks = row.ProductStocks.Concat(row.MaterialStocks)
            .Where(x => request.StockTypes.Length == 0 || request.StockTypes.Contains(x.StockType)).ToArray();
        if (request.StockTypes.Length > 0 && stocks.Length == 0) return false;
        if (request.OnlyAvailableLeZero && !stocks.Any(x => x.AvailableKg <= 0) && stocks.Length > 0) return false;
        return request.AvailableMax is not decimal max || stocks.Length == 0 || stocks.Any(x => x.AvailableKg <= max);
    }
}

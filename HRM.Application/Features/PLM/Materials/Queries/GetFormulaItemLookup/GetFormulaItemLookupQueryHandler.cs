using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Materials.Dtos.Lookup;
using HRM.Application.Features.PLM.Materials.Rules;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Materials;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetFormulaItemLookup;

internal sealed class GetFormulaItemLookupQueryHandler
    : IRequestHandler<GetFormulaItemLookupQuery, PagedResult<FormulaItemLookupDto>>
{
    private const string ProductCategoryCode = "KH";

    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMaterialPriceQueryService _priceQueryService;

    public GetFormulaItemLookupQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IMaterialPriceQueryService priceQueryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _priceQueryService = priceQueryService;
    }

    public async Task<PagedResult<FormulaItemLookupDto>> Handle(
        GetFormulaItemLookupQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return EmptyResult(request);
        }

        var itemType = NormalizeItemType(request.ItemType);
        var includeMaterials = itemType is null or ItemType.Material;
        var includeProducts = itemType is null or ItemType.Product;

        if (!includeMaterials && !includeProducts)
        {
            return EmptyResult(request);
        }

        var (keyword, processingType) = ProcessingLookupSearch.Parse(request.NormalizedKeyword);
        var sampleSentStatus = SampleRequestStatus.SampleSent.ToString();
        var completedStatus = SampleRequestStatus.Completed.ToString();

        var materials = _dbContext.Materials
            .AsNoTracking()
            .Where(x =>
                includeMaterials &&
                x.CompanyId == companyId &&
                x.IsActive == true &&
                (request.ProcessingAsMaterial || !_dbContext.Products.Any(p => p.CompanyId == companyId && p.IsActive &&
                    p.ColourCode != null && p.ColourCode == x.ExternalId &&
                    p.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive && s.RequestType == SemiFinishedProcessing.RequestType))));

        var products = _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                (includeProducts || includeMaterials && x.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive && s.RequestType == "SemiFinished")) &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Name != null &&
                x.ColourCode != null &&
                x.SampleRequests.Any(sr =>
                    sr.IsActive &&
                    sr.CompanyId == companyId && (sr.RequestType == "SemiFinished" || sr.Status == sampleSentStatus || sr.Status == completedStatus)));

        if (request.ProcessingAsMaterial)
        {
            var processed = products.Where(x =>
                (x.ColourCode!.Trim().ToUpper().EndsWith("-N") || x.ColourCode.Trim().ToUpper().EndsWith("-B")) &&
                x.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive && s.RequestType == SemiFinishedProcessing.RequestType));
            products = products.Where(x => processed.Any(p => p.ProductId == x.ProductId)
                ? includeMaterials && !_dbContext.Materials.Any(m =>
                    m.CompanyId == companyId && m.IsActive == true && m.ExternalId == x.ColourCode)
                : includeProducts);
        }

        if (request.CategoryId is { } categoryId && categoryId != Guid.Empty)
        {
            materials = materials.Where(x => x.CategoryId == categoryId);
            products = products.Where(x => x.CategoryId == categoryId);
        }

        if (processingType is not null)
        {
            var suffix = "-" + processingType;
            var legacySuffix = "_" + processingType;
            materials = materials.Where(x => x.ExternalId != null &&
                (x.ExternalId.Trim().ToUpper().EndsWith(suffix) || x.ExternalId.Trim().ToUpper().EndsWith(legacySuffix)));
            products = products.Where(x => x.ColourCode != null &&
                (x.ColourCode.Trim().ToUpper().EndsWith(suffix) || x.ColourCode.Trim().ToUpper().EndsWith(legacySuffix)));
        }

        if (keyword is not null)
        {
            materials = materials.Where(x =>
                EF.Functions.ILike((x.ExternalId ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.Name ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.CustomCode ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter));

            products = products.Where(x =>
                EF.Functions.ILike((x.ColourCode ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.Name ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.Code ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                x.SampleRequests.Any(sampleRequest =>
                    sampleRequest.IsActive &&
                    sampleRequest.CompanyId == companyId &&
                    EF.Functions.ILike(sampleRequest.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)) ||
                x.Formulas.Any(formula =>
                    formula.IsActive &&
                    formula.CompanyId == companyId &&
                    EF.Functions.ILike(formula.ExternalId, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)));
        }

        var materialRows = materials.Select(x => new FormulaItemLookupRow
        {
            ItemId = x.MaterialId,
            ItemType = ItemType.Material,
            ExternalId = x.ExternalId,
            StockCode = x.ExternalId,
            CustomCode = x.CustomCode,
            Name = x.Name ?? string.Empty,
            CategoryId = x.CategoryId,
            CategoryCode = x.Category.ExternalId,
            Weight = x.Weight,
            Package = x.Package,
            Unit = x.Unit,
            PurchaseStatus = x.PurchaseAvailability == null
                ? null
                : x.PurchaseAvailability.Status,
            PurchaseStatusReason = x.PurchaseAvailability == null
                ? null
                : x.PurchaseAvailability.Reason,
            PurchaseStatusEffectiveFrom = x.PurchaseAvailability == null
                ? null
                : x.PurchaseAvailability.EffectiveFrom,
            ExpectedAvailableDate = x.PurchaseAvailability == null
                ? null
                : x.PurchaseAvailability.ExpectedAvailableDate,
            CreatedDate = x.CreatedDate,
            ProcessingType = null,
            ProcessingAlias = request.ProcessingAsMaterial && x.ExternalId != null &&
                (x.ExternalId.Trim().ToUpper().EndsWith("-N") || x.ExternalId.Trim().ToUpper().EndsWith("-B")) &&
                _dbContext.Products.Any(p => p.CompanyId == companyId && p.IsActive &&
                    p.ColourCode == x.ExternalId &&
                    p.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive && s.RequestType == SemiFinishedProcessing.RequestType))
        });

        var productRows = products.Select(x => new FormulaItemLookupRow
        {
            ItemId = x.ProductId,
            ItemType = ItemType.Product,
            ExternalId = x.SampleRequests
                .Where(sr =>
                    sr.IsActive &&
                    sr.CompanyId == companyId && (sr.RequestType == "SemiFinished" || sr.Status == sampleSentStatus || sr.Status == completedStatus))
                .OrderByDescending(sr => sr.CreatedDate)
                .Select(sr => sr.ExternalId)
                .FirstOrDefault(),
            CustomCode = x.Code,
            StockCode = x.ColourCode,
            Name = "[" + (x.ColourCode ?? string.Empty) + "] " + (x.Name ?? string.Empty),
            CategoryId = x.CategoryId,
            CategoryCode = x.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive && s.RequestType == "SemiFinished")
                ? x.Category != null && x.Category.CompanyId == companyId ? x.Category.ExternalId : null : ProductCategoryCode,
            Weight = x.Weight,
            Package = null,
            Unit = x.Unit,
            PurchaseStatus = null,
            PurchaseStatusReason = null,
            PurchaseStatusEffectiveFrom = null,
            ExpectedAvailableDate = null,
            CreatedDate = x.CreatedDate,
            ProcessingType = null,
            ProcessingAlias = request.ProcessingAsMaterial &&
                (x.ColourCode!.Trim().ToUpper().EndsWith("-N") || x.ColourCode.Trim().ToUpper().EndsWith("-B")) &&
                x.SampleRequests.Any(s => s.CompanyId == companyId && s.IsActive && s.RequestType == SemiFinishedProcessing.RequestType)
        });

        IQueryable<FormulaItemLookupRow> PreviewRows(string process)
        {
            var suffix = "-" + process;
            return _dbContext.Materials.AsNoTracking().Where(x => includeMaterials && x.CompanyId == companyId && x.IsActive == true
                && x.Category.CompanyId == companyId && x.Category.IsActive == true && x.Category.Types == "NVL"
                && x.ExternalId != null && x.ExternalId.Trim().Length <= 254 && x.ExternalId.Trim() != ""
                && !System.Text.RegularExpressions.Regex.IsMatch(x.ExternalId.Trim(), MaterialProcessingRules.ExistingSuffixPattern)
                && x.Unit != null && x.Unit.Trim().ToLower() == "kg" && (x.Name == null || x.Name.Length <= 1000)
                && !x.ExternalId.Trim().ToUpper().EndsWith("-N") && !x.ExternalId.Trim().ToUpper().EndsWith("-B")
                && !x.ExternalId.Trim().ToUpper().EndsWith("-L") && !x.ExternalId.Trim().ToUpper().EndsWith("_N")
                && !x.ExternalId.Trim().ToUpper().EndsWith("_B") && !x.ExternalId.Trim().ToUpper().EndsWith("_L")
                && (!request.CategoryId.HasValue || request.CategoryId == Guid.Empty || x.CategoryId == request.CategoryId)
                && (keyword == null || EF.Functions.ILike(x.ExternalId + suffix, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)
                    || EF.Functions.ILike(x.Name ?? "", PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)
                    || EF.Functions.ILike(x.CustomCode ?? "", PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter))
                && !_dbContext.Products.Any(p => p.CompanyId == companyId && p.ColourCode != null && p.ColourCode.Trim().ToUpper() == (x.ExternalId.Trim() + suffix).ToUpper())
                && !_dbContext.Materials.Any(m => m.CompanyId == companyId && m.ExternalId != null && m.ExternalId.Trim().ToUpper() == (x.ExternalId.Trim() + suffix).ToUpper())
                && !_dbContext.SampleRequests.Any(s => s.CompanyId == companyId && s.ExternalId.ToUpper() == (x.ExternalId.Trim() + suffix).ToUpper()))
                .Select(x => new FormulaItemLookupRow {
                    ItemId = x.MaterialId, ItemType = ItemType.Product, ExternalId = x.ExternalId!.Trim() + suffix,
                    StockCode = x.ExternalId.Trim() + suffix, CustomCode = x.CustomCode,
                    Name = (x.Name ?? x.ExternalId) + (process == "N" ? " (Nghiền)" : " (Băm)"),
                    CategoryId = x.CategoryId, CategoryCode = x.Category.ExternalId, Weight = x.Weight, Unit = x.Unit,
                    Package = null, PurchaseStatus = null, PurchaseStatusReason = null,
                    PurchaseStatusEffectiveFrom = null, ExpectedAvailableDate = null,
                    ProcessingType = process, CreatedDate = x.CreatedDate, ProcessingAlias = request.ProcessingAsMaterial
                });
        }
        var query = materialRows.Concat(productRows);
        if (processingType is not null)
            query = query.Concat(PreviewRows(processingType));
        // Count only identities: no latest sample-request/category/availability projection is needed.
        var totalCount = await query.Select(x => x.ItemId).CountAsync(cancellationToken);
        if (totalCount == 0) return EmptyResult(request);

        var rows = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenBy(x => x.ItemType)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.ItemId)
            .ThenBy(x => x.ProcessingType)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        var prices = rows.Count == 0
            ? []
            : await _priceQueryService.LoadLatestPricingItemPriceInfoDictAsync(
                companyId,
                "VND",
                rows.Where(x => x.ProcessingType == null).Select(x => new PriceItemRequest
                {
                    ItemType = x.ItemType,
                    MaterialId = x.ItemType == ItemType.Material ? x.ItemId : null,
                    ProductId = x.ItemType == ItemType.Product ? x.ItemId : null
                }),
                cancellationToken);

        var items = rows
            .Select(x =>
            {
                var unitPrice = MaterialPriceResolver.ResolveLatestItemPrice(
                    prices,
                    x.ItemType,
                    x.ItemId,
                    0m);

                return new FormulaItemLookupDto
                {
                    ItemId = x.ProcessingType is null ? x.ItemId : ProcessingPreviewIdentity.Id(x.ItemId, x.ProcessingType),
                    IsProcessingPreview = x.ProcessingType is not null,
                    IsProcessedMaterial = x.ProcessingAlias,
                    ItemType = x.ProcessingAlias ? ItemType.Material : x.ItemType,
                    ExternalId = x.ExternalId,
                    StockCode = x.StockCode,
                    CustomCode = x.CustomCode,
                    Name = x.Name,
                    Label = BuildLabel(x),
                    CategoryId = x.CategoryId,
                    CategoryCode = x.CategoryCode,
                    Weight = x.Weight,
                    Package = x.Package,
                    Unit = x.Unit,
                    PurchaseAvailability = x.ItemType == ItemType.Material
                        ? MaterialPurchaseAvailabilityRules.Resolve(
                            x.PurchaseStatus,
                            x.PurchaseStatusReason,
                            x.PurchaseStatusEffectiveFrom,
                            x.ExpectedAvailableDate)
                        : null,
                    Price = new LatestPriceSource
                    {
                        UnitPrice = unitPrice,
                        LatestPriceDate = MaterialPriceResolver.ResolveLatestItemPriceDate(
                            prices,
                            x.ItemType,
                            x.ItemId),
                        Source = MaterialPriceResolver.ResolveLatestItemPriceSource(
                            prices,
                            x.ItemType,
                            x.ItemId),
                        Calculation = prices.GetValueOrDefault(
                            new PriceItemKey(x.ItemType, x.ItemId))?.Calculation
                    }
                };
            })
            .ToList();

        return new PagedResult<FormulaItemLookupDto>(
            items,
            totalCount,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private static ItemType? NormalizeItemType(ItemType? itemType)
    {
        return itemType switch
        {
            ItemType.MaterialFailure => ItemType.Material,
            ItemType.ProductFailure => ItemType.Product,
            _ => itemType
        };
    }

    private static string BuildLabel(FormulaItemLookupRow row)
    {
        var code = string.IsNullOrWhiteSpace(row.ExternalId)
            ? row.CustomCode
            : row.ExternalId;

        return string.IsNullOrWhiteSpace(code)
            ? row.Name
            : $"{code} - {row.Name}";
    }

    private static PagedResult<FormulaItemLookupDto> EmptyResult(
        GetFormulaItemLookupQuery request)
    {
        return new PagedResult<FormulaItemLookupDto>(
            [],
            0,
            request.NormalizedPageNumber,
            request.NormalizedPageSize);
    }

    private sealed class FormulaItemLookupRow
    {
        public Guid ItemId { get; init; }
        public ItemType ItemType { get; init; }
        public string? ExternalId { get; init; }
        public string? StockCode { get; init; }
        public string? CustomCode { get; init; }
        public string Name { get; init; } = string.Empty;
        public Guid? CategoryId { get; init; }
        public bool ProcessingAlias { get; init; }
        public string? CategoryCode { get; init; }
        public double? Weight { get; init; }
        public string? Package { get; init; }
        public string? Unit { get; init; }
        public MaterialPurchaseStatus? PurchaseStatus { get; init; }
        public string? PurchaseStatusReason { get; init; }
        public DateTime? PurchaseStatusEffectiveFrom { get; init; }
        public DateTime? ExpectedAvailableDate { get; init; }
        public DateTime? CreatedDate { get; init; }
        public string? ProcessingType { get; init; }
    }
}

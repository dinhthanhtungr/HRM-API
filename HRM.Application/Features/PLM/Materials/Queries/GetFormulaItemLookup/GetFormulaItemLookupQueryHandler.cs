using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Helpers;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Materials.Dtos.Lookup;
using HRM.Domain.Enums.Formulas;
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

        var keyword = request.NormalizedKeyword;
        var sampleSentStatus = SampleRequestStatus.SampleSent.ToString();
        var completedStatus = SampleRequestStatus.Completed.ToString();

        var materials = _dbContext.Materials
            .AsNoTracking()
            .Where(x =>
                includeMaterials &&
                x.CompanyId == companyId &&
                x.IsActive == true);

        var products = _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                includeProducts &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Name != null &&
                x.ColourCode != null &&
                x.SampleRequests.Any(sr =>
                    sr.IsActive &&
                    (sr.Status == sampleSentStatus || sr.Status == completedStatus)));

        if (request.CategoryId is { } categoryId && categoryId != Guid.Empty)
        {
            materials = materials.Where(x => x.CategoryId == categoryId);
            products = products.Where(x => x.CategoryId == categoryId);
        }

        if (keyword is not null)
        {
            materials = materials.Where(x =>
                (x.ExternalId ?? string.Empty).Contains(keyword) ||
                (x.Name ?? string.Empty).Contains(keyword) ||
                (x.CustomCode ?? string.Empty).Contains(keyword));

            products = products.Where(x =>
                (x.ColourCode ?? string.Empty).Contains(keyword) ||
                (x.Name ?? string.Empty).Contains(keyword) ||
                (x.Code ?? string.Empty).Contains(keyword));
        }

        var materialRows = materials.Select(x => new FormulaItemLookupRow
        {
            ItemId = x.MaterialId,
            ItemType = ItemType.Material,
            ExternalId = x.ExternalId,
            CustomCode = x.CustomCode,
            Name = x.Name ?? string.Empty,
            CategoryId = x.CategoryId,
            CategoryCode = x.Category.ExternalId,
            Weight = x.Weight,
            Package = x.Package,
            Unit = x.Unit,
            CreatedDate = x.CreatedDate
        });

        var productRows = products.Select(x => new FormulaItemLookupRow
        {
            ItemId = x.ProductId,
            ItemType = ItemType.Product,
            ExternalId = x.SampleRequests
                .Where(sr =>
                    sr.IsActive &&
                    (sr.Status == sampleSentStatus || sr.Status == completedStatus))
                .OrderByDescending(sr => sr.CreatedDate)
                .Select(sr => sr.ExternalId)
                .FirstOrDefault(),
            CustomCode = x.Code,
            Name = "[" + (x.ColourCode ?? string.Empty) + "] " + (x.Name ?? string.Empty),
            CategoryId = x.CategoryId,
            CategoryCode = ProductCategoryCode,
            Weight = x.Weight,
            Package = null,
            Unit = x.Unit,
            CreatedDate = x.CreatedDate
        });

        var query = materialRows.Concat(productRows);
        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.CreatedDate)
            .ThenBy(x => x.ItemType)
            .ThenBy(x => x.Name)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize)
            .Take(request.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        var prices = rows.Count == 0
            ? []
            : await _priceQueryService.LoadLatestItemPriceInfoDictAsync(
                rows.Select(x => new PriceItemRequest
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
                    ItemId = x.ItemId,
                    ItemType = x.ItemType,
                    ExternalId = x.ExternalId,
                    CustomCode = x.CustomCode,
                    Name = x.Name,
                    Label = BuildLabel(x),
                    CategoryId = x.CategoryId,
                    CategoryCode = x.CategoryCode,
                    Weight = x.Weight,
                    Package = x.Package,
                    Unit = x.Unit,
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
                            x.ItemId)
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
        public string? CustomCode { get; init; }
        public string Name { get; init; } = string.Empty;
        public Guid? CategoryId { get; init; }
        public string? CategoryCode { get; init; }
        public double? Weight { get; init; }
        public string? Package { get; init; }
        public string? Unit { get; init; }
        public DateTime? CreatedDate { get; init; }
    }
}

using HRM.Application.Abstractions.Commons.Pricing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Pricing.Dtos;
using HRM.Application.Commons.Pricing.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Queries.GetManufacturingFormulaMaterials;

internal sealed class GetManufacturingFormulaMaterialsQueryHandler
    : IRequestHandler<GetManufacturingFormulaMaterialsQuery, FormulaDto>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly IMaterialPriceQueryService _materialPriceQueryService;

    public GetManufacturingFormulaMaterialsQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IPLMFieldVisibilityService fieldVisibility,
        IMaterialPriceQueryService materialPriceQueryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fieldVisibility = fieldVisibility;
        _materialPriceQueryService = materialPriceQueryService;
    }

    public async Task<FormulaDto> Handle(
        GetManufacturingFormulaMaterialsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return EmptyResult(request.ManufacturingFormulaId);
        }

        var formulaHeader = await _dbContext.ManufacturingFormulas
            .AsNoTracking()
            .Where(x =>
                x.ManufacturingFormulaId == request.ManufacturingFormulaId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                FormulaId = x.ManufacturingFormulaId,
                ExternalId = x.ExternalId,
                Note = x.Note ?? string.Empty,
                x.CompanyId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (formulaHeader is null)
        {
            return EmptyResult(request.ManufacturingFormulaId);
        }

        var items = await _dbContext.ManufacturingFormulaMaterials
            .AsNoTracking()
            .Where(x => x.IsActive == true && x.ManufacturingFormulaId == request.ManufacturingFormulaId)
            .OrderBy(x => x.LineNo)
            .Select(x => new FormulaMaterialDto
            {
                FormulaMaterialId = x.ManufacturingFormulaMaterialId,
                LineNo = x.LineNo,
                ItemId = x.MaterialId ?? x.ProductId ?? Guid.Empty,
                ItemType = x.itemType,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,

                MaterialNameSnapshot = x.MaterialNameSnapshot,
                MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot
            })
            .ToListAsync(cancellationToken);

        var currentItemData = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            _dbContext,
            formulaHeader.CompanyId,
            items.Select(item => new FormulaItemDisplaySource(
                item.ItemId,
                item.ItemType,
                item.MaterialNameSnapshot,
                item.MaterialExternalIdSnapshot)),
            cancellationToken);

        foreach (var item in items)
        {
            var display = FormulaItemDisplayResolver.Resolve(
                new FormulaItemDisplaySource(
                    item.ItemId,
                    item.ItemType,
                    item.MaterialNameSnapshot,
                    item.MaterialExternalIdSnapshot),
                currentItemData);
            item.MaterialNameSnapshot = display.Name;
            item.MaterialExternalIdSnapshot = display.ExternalId;
        }

        if (_fieldVisibility.CanViewFormulaPrices())
        {
            var priceRequests = items
                .Where(x => x.ItemId != Guid.Empty)
                .Select(x => new PriceItemRequest
                {
                    ItemType = NormalizePriceItemType(x.ItemType),
                    MaterialId = IsMaterial(x.ItemType) ? x.ItemId : null,
                    ProductId = IsMaterial(x.ItemType) ? null : x.ItemId
                })
                .ToList();

            var latestPriceByItem = await _materialPriceQueryService
                .LoadLatestPricingItemPriceInfoDictAsync(
                    formulaHeader.CompanyId,
                    "VND",
                    priceRequests,
                    cancellationToken);

            foreach (var item in items)
            {
                ApplyRealtimePrice(item, latestPriceByItem);
            }
        }

        return new FormulaDto
        {
            FormulaId = formulaHeader.FormulaId,
            ExternalId = formulaHeader.ExternalId,
            Note = formulaHeader.Note,
            Items = items
        };
    }

    private static FormulaDto EmptyResult(Guid formulaId) => new()
    {
        FormulaId = formulaId,
        Items = []
    };

    private static void ApplyRealtimePrice(
        FormulaMaterialDto item,
        IReadOnlyDictionary<PriceItemKey, LatestItemPriceDto> latestPriceByItem)
    {
        var normalizedItemType = NormalizePriceItemType(item.ItemType);
        LatestItemPriceDto? latestPrice = null;
        var hasLatestPrice = item.ItemId != Guid.Empty &&
            latestPriceByItem.TryGetValue(
                new PriceItemKey(normalizedItemType, item.ItemId),
                out latestPrice) &&
            latestPrice.PriceSource != LatestPriceSourceType.Unknown;

        var latestUnitPrice = hasLatestPrice ? latestPrice!.CurrentPrice : 0m;
        var latestTotalPrice = decimal.Round(item.Quantity * latestUnitPrice, 6, MidpointRounding.AwayFromZero);
        var latestPriceDate = hasLatestPrice ? latestPrice!.PriceDate : null;
        var latestPriceSource = hasLatestPrice
            ? latestPrice!.PriceSource
            : LatestPriceSourceType.Unknown;

        item.HasLatestPrice = hasLatestPrice;
        item.LatestUnitPrice = latestUnitPrice;
        item.LatestTotalPrice = latestTotalPrice;
        item.LatestPriceDate = latestPriceDate;
        item.LatestPriceSource = latestPriceSource;
        item.Price = new LatestPriceSource
        {
            UnitPrice = latestUnitPrice,
            LatestPriceDate = latestPriceDate,
            Source = latestPriceSource
        };
        item.PriceTotal = latestTotalPrice;
    }

    private static bool IsMaterial(ItemType itemType) =>
        itemType is ItemType.Material or ItemType.MaterialFailure;

    private static ItemType NormalizePriceItemType(ItemType itemType) => itemType switch
    {
        ItemType.MaterialFailure => ItemType.Material,
        ItemType.ProductFailure => ItemType.Product,
        _ => itemType
    };
}
